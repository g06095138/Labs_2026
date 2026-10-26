from pathlib import Path
import json,os
from playwright.sync_api import sync_playwright,expect
out=Path(__file__).resolve().parents[1]/'docs'/'test-results'
data=json.loads((out/'browser-smoke.json').read_text());oid=data['order_id']
with sync_playwright() as p:
 b=p.chromium.launch(executable_path=os.environ.get('CHROMIUM','/usr/bin/chromium'),headless=True,args=['--no-sandbox'])
 page=b.new_page(viewport={'width':1440,'height':1100},locale='ru-RU')
 page.goto(os.environ.get('TAXI_URL','http://127.0.0.1:5080')+'/login');page.get_by_label('Логин',exact=True).fill('client');page.get_by_label('Пароль',exact=True).fill('TaxiDemo!2026');page.get_by_role('button',name='Войти',exact=True).click()
 expect(page.locator('tbody tr')).to_have_count(2)
 row=page.locator('tr').filter(has=page.locator('td[title="'+oid+'"]'));expect(row).to_contain_text('Завершён');expect(row).to_contain_text('270,00')
 page.screenshot(path=str(out/'after-restart.png'),full_page=True)
 b.close()
(out/'restart-check.json').write_text(json.dumps({'passed':True,'order_id':oid,'order_count':2,'status':'Completed','cost':'270.00','method':'New server process, same SQLite file, new browser login'},indent=2))
print('PASS: server restart preserves both orders, Completed status and 270.00 price')
