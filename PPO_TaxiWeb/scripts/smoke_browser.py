"""Optional browser smoke: pip install playwright; install Chromium or set CHROMIUM.
Run against an isolated fresh database: python scripts/smoke_browser.py
"""
import os,json
from pathlib import Path
from playwright.sync_api import sync_playwright,expect
base=os.environ.get('TAXI_URL','http://127.0.0.1:5080')
out=Path(__file__).resolve().parents[1]/'docs'/'test-results';out.mkdir(parents=True,exist_ok=True)
checks=[]
def ok(name):checks.append(name);print('PASS:',name,flush=True)
with sync_playwright() as p:
 browser=p.chromium.launch(executable_path=os.environ.get('CHROMIUM','/usr/bin/chromium'),headless=True,args=['--no-sandbox'])
 context=browser.new_context(viewport={'width':1440,'height':1100},locale='ru-RU')
 page=context.new_page()
 errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 def login(user,password='TaxiDemo!2026'):
  page.goto(base+'/login');page.get_by_label('Логин',exact=True).fill(user);page.get_by_label('Пароль',exact=True).fill(password);page.get_by_role('button',name='Войти',exact=True).click()
 def logout():
  page.get_by_role('button',name='Выйти',exact=True).click();expect(page.get_by_role('heading',name='Вход в систему')).to_be_visible()
 def fill_trip(distance='4'):
  page.get_by_label('ФИО',exact=True).fill('Преснов Глеб');page.get_by_label('Телефон',exact=True).fill('+79991234567')
  page.get_by_label('Откуда',exact=True).fill('Вокзал');page.get_by_label('Куда',exact=True).fill('Университет')
  page.get_by_label('Расстояние, км',exact=True).fill(distance)
 def compare():page.get_by_role('button',name='Сравнить тарифы',exact=True).click()
 page.goto(base+'/');expect(page.get_by_role('heading',name='Вход в систему')).to_be_visible();ok('Unauthenticated home denied')
 login('client','wrong');expect(page.get_by_role('alert')).to_have_text('Неверный логин или пароль.');ok('Invalid login rejected')
 login('client');expect(page.get_by_role('heading',name='Заказать поездку')).to_be_visible()
 fill_trip();compare();expect(page.get_by_role('radio',name='Комфорт',exact=True)).to_be_visible()
 expect(page.locator('tr').filter(has=page.get_by_role('radio',name='Комфорт',exact=True))).to_contain_text('270,00');ok('Comparison 4 km: comfort 270.00')
 page.screenshot(path=str(out/'client-comparison.png'),full_page=True)
 page.get_by_label('Расстояние, км',exact=True).fill('10');expect(page.get_by_role('radio',name='Комфорт',exact=True)).to_have_count(0);ok('Editing hides stale quote')
 compare();expect(page.locator('tr').filter(has=page.get_by_role('radio',name='Комфорт',exact=True))).to_contain_text('450,00');ok('Recalculation 10 km: comfort 450.00')
 page.get_by_label('Расстояние, км',exact=True).fill('0');compare();expect(page.get_by_role('alert')).to_contain_text('Расстояние');expect(page.get_by_role('radio')).to_have_count(0);ok('Invalid distance rejected without stale result')
 page.get_by_label('Расстояние, км',exact=True).fill('4');compare();page.get_by_role('radio',name='Комфорт',exact=True).check();page.get_by_role('button',name='Создать заказ',exact=True).click();expect(page.get_by_role('status')).to_contain_text('создан')
 order_id=page.locator('tbody tr').first.locator('td').first.get_attribute('title');assert order_id
 expect(page.locator('tbody tr')).to_have_count(1);ok('Order created at fixed 270.00')
 compare();page.get_by_role('button',name='Создать заказ',exact=True).click();expect(page.locator('tbody tr')).to_have_count(2);ok('Second independent order created')
 logout();login('client2');expect(page.get_by_text('Заказов пока нет.',exact=True)).to_be_visible();ok('Other client cannot see first client orders');logout()
 login('dispatcher');expect(page.get_by_role('heading',name='Рабочее место диспетчера')).to_be_visible()
 page.get_by_label('Свободный водитель',exact=True).select_option('driver-1')
 row=page.locator('tr').filter(has=page.locator('td[title="'+order_id+'"]'))
 row.get_by_role('button',name='Назначить',exact=True).click();expect(row).to_contain_text('Водитель назначен')
 assert page.get_by_label('Свободный водитель',exact=True).locator('option[value="driver-1"]').count()==0
 page.screenshot(path=str(out/'dispatcher-orders.png'),full_page=True);ok('Dispatcher assigns and busy driver disappears');logout()
 login('driver2');expect(page.get_by_text('Заказов пока нет.',exact=True)).to_be_visible();ok('Other driver cannot see assigned order');logout()
 login('driver1');expect(page.locator('tbody tr')).to_have_count(1);page.get_by_role('button',name='Начать',exact=True).click();expect(page.locator('tbody tr')).to_contain_text('В пути')
 page.screenshot(path=str(out/'driver-trip.png'),full_page=True)
 page.get_by_role('button',name='Завершить',exact=True).click();expect(page.get_by_text('Заказов пока нет.',exact=True)).to_be_visible();ok('Driver starts and completes own order');logout()
 login('dispatcher');expect(page.get_by_label('Свободный водитель',exact=True).locator('option[value="driver-1"]')).to_have_count(1);ok('Completed trip releases driver');logout()
 login('client');row=page.locator('tr').filter(has=page.locator('td[title="'+order_id+'"]'));expect(row).to_contain_text('Завершён');expect(row).to_contain_text('270,00');ok('Client sees completed order at original price')
 assert not errors,errors;ok('No browser page errors')
 browser.close()
(out/'browser-smoke.json').write_text(json.dumps({'checks':checks,'count':len(checks),'order_id':order_id,'browser_errors':errors},ensure_ascii=False,indent=2))
print('Browser smoke complete:',len(checks))
