#!/usr/bin/env python3
"""Build the concise Russian LR3 report from the project contract and diagrams."""
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, Cm, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'reports' / 'ЛР3_Оператор_связи.docx'
doc = Document()
section = doc.sections[0]
section.page_width, section.page_height = Cm(21), Cm(29.7)
section.top_margin = section.bottom_margin = Cm(1.7)
section.left_margin = section.right_margin = Cm(1.8)
normal = doc.styles['Normal']
normal.font.name = 'DejaVu Sans'
normal.font.size = Pt(10)
normal.paragraph_format.space_after = Pt(5)
normal.paragraph_format.line_spacing = 1.05
for name, size in [('Title', 19), ('Heading 1', 15), ('Heading 2', 12)]:
    style = doc.styles[name]
    style.font.name = 'DejaVu Sans'
    style.font.size = Pt(size)
    style.font.color.rgb = RGBColor.from_string('1F3449')
    style.paragraph_format.space_after = Pt(7)
    style.paragraph_format.space_before = Pt(6)
header = section.header.paragraphs[0]
header.text = 'ППО • ЛР3 • Учёт вызовов оператора связи'
header.style = doc.styles['Caption']
footer = section.footer.paragraphs[0]
footer.alignment = 2
footer.add_run('ЛР3 • ')
fld = OxmlElement('w:fldSimple'); fld.set(qn('w:instr'), 'PAGE'); footer._p.append(fld)

def para(text, bold=False):
    p=doc.add_paragraph(); p.add_run(text).bold=bold; return p

def table(headers, rows, widths=None):
    t=doc.add_table(rows=1, cols=len(headers));t.style='Light Shading Accent 1'
    for c,s in zip(t.rows[0].cells,headers): c.text=str(s)
    for row in rows:
        for c,s in zip(t.add_row().cells,row): c.text=str(s)
    for row in t.rows:
        for cell in row.cells:
            for p in cell.paragraphs:
                p.paragraph_format.space_after=Pt(2)
                for r in p.runs: r.font.size=Pt(8.5)
    if widths:
        for row in t.rows:
            for cell,w in zip(row.cells,widths):cell.width=Cm(w)
    return t

def heading(text): doc.add_heading(text, level=1)
def page(): doc.add_page_break()
def picture(name, maxw=6.7, maxh=4.0):
    p=ROOT/'docs/diagrams'/name
    if p.exists():
        w,h=Image.open(p).size
        scale=min(maxw/w,maxh/h)
        doc.add_picture(str(p),width=Inches(w*scale),height=Inches(h*scale))
    else: para('Диаграмма: docs/diagrams/'+name)

# Page 1
p=doc.add_paragraph();p.style='Title';p.add_run('ЛАБОРАТОРНАЯ РАБОТА 3\nПрогноз и финансовые показатели')
para('Тема: финансовое планирование оператора связи', True)
para('На основе курсовой «БД „Учёт вызовов оператора связи“»')
para('Исполнитель: ____________________    Группа: ____________')
heading('1. Цель и предметная адаптация')
para('Разработать расчёт финансового плана на три месяца и независимое сравнение двух планов. Прогноз должен показать, хватит ли оператору денег на обслуживание абонентов, даже при положительной прибыли.')
para('Курсовая содержит клиентов, вызовы, тарифы, страны, города и платежи. Цена минуты и абонентская плата связаны с её тарифной моделью. Для ЛР3 используются агрегированные оплачиваемые минуты после правил тарифа; отдельные вызовы не тарифицируются. Затраты и доля оплаты — дополнительные плановые предположения.')
heading('2. Входной план')
table(['Поле','Смысл и ограничение'],[
('Id, Name','Непустой UUID; название после trim — 1…200 символов'),
('InitialCash','Деньги до первого месяца, неотрицательные'),
('StartYear, StartMonth','Начало плана; все три календарных периода — 2020…2099'),
('Number','Ровно три уникальных номера 1, 2, 3'),
('ActiveClients','Обслуженные абоненты Q; целое 0…10⁶'),
('AvgBillableMinutesPerClient','Оплачиваемые минуты на абонента; целое 0…10⁶'),
('PricePerMinute; MonthlyFeePerClient','Цена минуты и месячная абонплата'),
('VariableCostPerClient; OperationalCosts','Переменные расходы на абонента V; постоянные расходы F'),
('PaymentPercent','Доля оплаты текущего месяца A; целое 0…100')])
para('Денежные входы — decimal 0…10¹² ₽, до двух десятичных знаков. Начальная задолженность равна нулю; перенесённые отрицательные деньги допустимы. Сохранение и веб-экран относятся к ЛР4 и ЛР5.')

# Page 2
page();heading('3. Формулы и ручной расчёт')
para('Средний счёт P = оплачиваемые минуты × цена минуты + абонплата. Для Q абонентов финансовые правила П-Б1–П-Б9 сохраняются:')
table(['Показатель','Формула'],[
('Выручка / переменные расходы','R = Q × P; переменные расходы = Q × V'),
('Маржинальный доход','R − Q × V'),
('Все расходы / прибыль','E = Q × V + F; прибыль = R − E'),
('Текущая оплата / долг','S = Round(R × A / 100, 2, AwayFromZero); D = R − S'),
('Поступления','S + долг на начало месяца'),
('Конечные деньги','C = деньги на начало + поступления − E'),
('Следующий месяц','Полученные C и D становятся начальными значениями'),
('Итоги','Прибыль = сумма за три месяца; финансирование = max(0, −min(C1,C2,C3))')])
para('C < 0 означает «Не хватает денег»; программа продолжает прогноз без автоматического кредита. При C = 0 дефицита нет. Неоплаченную выручку третьего месяца к деньгам не прибавляют.')
doc.add_heading('Контроль: оплата 50% сразу',level=2)
para('Начало — ноябрь 2026; C0 = 50000. Каждый месяц: 100 клиентов, 200 минут по 5 ₽, абонплата 500 ₽, V = 500 ₽, F = 70000 ₽. P = 1500; R = 150000; переменные расходы = 50000; маржинальный доход = 100000; E = 120000; прибыль = 30000; S = 75000.')
table(['Показатель','Ноябрь 2026','Декабрь 2026','Январь 2027'],[
('Начальные деньги','50000','5000','35000'),('Начальный долг','0','75000','75000'),
('Текущая оплата','75000','75000','75000'),('Поступления','75000','150000','150000'),
('Все расходы','120000','120000','120000'),('Прибыль','30000','30000','30000'),
('Конечные деньги','5000','35000','65000'),('Конечный долг','75000','75000','75000')])
para('Итого: прибыль 90000; деньги 65000; долг 75000; финансирование 0. При оплате 0% деньги −70000 / −40000 / −10000: финансирование равно 70000, а не сумме отрицательных остатков 120000.')

# Page 3
page();heading('4. Приёмочные примеры')
para('Ожидания получены ручным расчётом до реализации. Общий ввод — как в разделе 3. Все подробные показатели, ссылки на правила и имена тестов находятся в docs/requirements.md.')
table(['Пример','Ввод / действие','Ожидаемый результат'],[
('ПР1','A = 0%','C: −70000/−40000/−10000; прибыль90000; долг150000; финансирование70000'),
('ПР2','A = 50%','C: 5000/35000/65000; прибыль90000; долг75000; финансирование0'),
('ПР3','A = 100%','C: 80000/110000/140000; прибыль90000; долг0; финансирование0'),
('ПР4','Q = 0; F = 70000','Выручка0; расходы70000; прибыль−70000 в месяц; C: −20000/−90000/−160000; финансирование160000'),
('ПР5','C0 = 75000; F = 100000; A = 50%','Прибыль0; C: 0/0/0; долг75000; финансирование0; дефицита нет'),
('ПР6','A второго месяца = 101%','Отказ с полем и причиной; полного прогноза нет'),
('ПР7','Сравнить ПР1/ПР2; затем C0 первого плана = 120000','Независимые итоги. Для изменённого первого плана C: 0/30000/60000, финансирование0'),
('ПР8','В ПР2 изменить A первого месяца на100%; затем сохранить/открыть в ЛР4','C: 80000/35000/65000; прибыль90000; долг75000. В ЛР3 проверяется расчёт; хранение — контракт ЛР4')])
doc.add_heading('Разные параметры месяцев',level=2)
para('C0 = 10000. М1: Q10, минуты100, цена20, абонплата1000, V1000, F5000, A50. М2: Q5, минуты100, цена30, абонплата1000, V1200, F6000, A0. М3: Q8, минуты100, цена20, абонплата500, V900, F7000, A100.')
table(['Показатель','М1','М2','М3'],[
('Выручка','30000','20000','20000'),('Все расходы','15000','12000','14200'),
('Прибыль','15000','8000','5800'),('Поступления','15000','15000','40000'),
('Конечные деньги','10000','13000','38800'),('Конечный долг','15000','20000','0')])
para('Общая прибыль 28800; конечные деньги 38800; долг0; финансирование0. При дробной оплате проверяется AwayFromZero: счёт0.01 ₽, A50% → оплата0.01 ₽, долг0.')

# Page 4
page();heading('5. Архитектура и диаграммы')
para('Domain содержит модели, валидацию и формулы. Application координирует прогноз и сравнение. Console читает входы и показывает результаты. Реализация Infrastructure относится к ЛР4; веб-интерфейс — к ЛР5.')
picture('components.png',maxw=6.7,maxh=2.1)
doc.add_paragraph('Рисунок 1 — Компоненты и направления зависимостей',style='Caption')
picture('domain.png',maxw=6.7,maxh=4.7)
doc.add_paragraph('Рисунок 2 — Классы плана, расчёта и результатов',style='Caption')
para('SRP: расчёт отделён от отображения. DIP: будущее хранилище реализует интерфейс Application; Domain не зависит от SQL. Модели входа неизменяемые, входной список копируется, результаты создаются заново. Исходники диаграмм находятся в docs/diagrams/*.puml.')

# Page 5
page();heading('6. Контракты и проверяемость')
table(['Операция','Контракт'],[
('PlanValidator.Validate','Проверяет полный план и все три месяца до расчёта; ошибка содержит Field и причину'),
('FinancialCalculator.CalculateMonth','Получает месяц, начальные деньги/долг; создаёт отчёт по формулам'),
('ForecastService.Forecast','Начинает с C0/D0=0, считает месяцы1/2/3, переносит C/D, формирует итоги'),
('ComparisonService.Compare','Проверяет две стороны с разными UUID, независимо рассчитывает их итоги'),
('Будущий IPlanRepository','SaveAsync, ListAsync, LoadAsync — контракт хранения для ЛР4')])
para('Недопустимый план не даёт частичного прогноза. Расчёт начинается с исходного входа при каждом вызове. Сравнение не меняет оба плана и не переносит задолженность между сторонами.')
doc.add_heading('Связь требований и тестов',level=2)
table(['Требования','Проверки xUnit'],[
('ЛР3-Т1: 0/50/100%','AcceptancePaymentPlans_MatchManualMonthlyAndFinalValues'),
('ЛР3-Т1: отсутствие клиентов','NoClients_MeansNoCallsFeesOrVariableExpenses_ButFixedCostsRemain'),
('ЛР3-Т1: нулевые деньги','ZeroClosingCash_IsValidAndNotDeficit'),
('ЛР3-Т1: недопустимый процент','InvalidPaymentPercent_IsRejectedWithFieldAndReason'),
('ЛР3-Т1/Т2: разные месяцы и переносы','VariedMonths_CarryDebtAndCash_UsingLiteralExpectedRows'),
('ЛР3-Т2: отрицательные остатки','NegativeCash_ContinuesThreeMonths_AndFundingIsWorstDeficit'),
('ЛР3-Т2: повторы, неизменность','RepeatedForecast_StartsFromOriginalCashAndZeroDebt_WithoutMutation'),
('ЛР3-Т2: сравнение','CompareIndependentPlans_ReturnsLiteralExpectedTotals_WithoutMutation'),
('ЛР3-Т3: отказы','InvalidInitialCash_IsRejected; EmptyOrWhitespaceName_IsRejected; DuplicateMissingOrOutOfRangeMonthNumbers_AreRejected'),
('Дополнительно: календарь/округление','ForecastCalendar_CrossesYearBoundary; PaymentRoundsAwayFromZero_AndDebtPreservesEveryKopeck')])
para('Полная таблица П-Ф1–П-Ф6 → сценарии → примеры → операции → тесты приведена в docs/requirements.md. Для П-Ф4 отдельно отмечено, что постоянное хранение и тесты перезапуска относятся к ЛР4. Финансовые формулы в UI не дублируются.')

# Page 6
page();heading('7. Запуск, результат и защита')
para('В VS Code откройте всю папку LR3_Telecom с TelecomFinance.sln. Требуются .NET 10 SDK и C# Dev Kit. Откройте src/TelecomFinance.Console/Program.cs и нажмите ▶; при выборе проекта укажите TelecomFinance.Console. Без аргументов выполняется демонстрация, затем программа завершает работу. В панели «Запуск и отладка» доступны готовые конфигурации.')
para('Тесты без команд: значок колбы «Тестирование» → «Запустить все тесты». При первом восстановлении пакетов нужен доступ к NuGet.')
doc.add_heading('Команды из папки решения',level=2)
for text in [
'dotnet restore TelecomFinance.sln --locked-mode',
'dotnet build TelecomFinance.sln -c Release --no-restore',
'dotnet run --project src/TelecomFinance.Console -c Release --no-build -- demo',
'dotnet test TelecomFinance.sln -c Release --no-build',
'dotnet run --project src/TelecomFinance.Console -- forecast examples/plan-b.json',
'dotnet run --project src/TelecomFinance.Console -- compare examples/plan-a.json examples/plan-b.json']:
    p=doc.add_paragraph();r=p.add_run(text);r.font.name='DejaVu Sans Mono';r.font.size=Pt(8)
doc.add_heading('Как объяснить полученный вывод',level=2)
para('Прибыль одинакова при 0%, 50% и 100% немедленной оплаты: объём услуг и расходы одинаковые. Различается срок поступления денег. Положительная прибыль не гарантирует достаточный денежный остаток. Финансирование покрывает самый большой дефицит, а не сумму минусов. Долг третьего месяца остаётся задолженностью.')
doc.add_heading('Что показать',level=2)
para('Требования и числовые примеры → диаграмма классов → FinancialCalculator.cs с формулой → ForecastService.cs с переносом → демонстрация → все тесты. Объяснить, как конкретное правило связано с методом и тестом. Подсказки для ответов находятся в docs/defense.md.')
doc.add_heading('Фактическая проверка и состав сдачи',level=2)
para('Сборка, автоматические тесты и консольные сценарии проверяются отдельно. Актуальные числа и результат запуска записаны в docs/validation.md; машинный протокол — docs/test-results. Настоящий отчёт не объявляет выполненным хранение после перезапуска или готовый веб-интерфейс.')
para('Сдаются исходники Domain/Application/Console, тесты, examples, requirements.md, design.md, диаграмма классов, этот отчёт и ссылка на коммит учебного Git-репозитория. Подробная адаптация исходной курсовой находится в docs/coursework-adaptation.md.')

OUT.parent.mkdir(parents=True,exist_ok=True)
doc.save(OUT)
print(OUT)
