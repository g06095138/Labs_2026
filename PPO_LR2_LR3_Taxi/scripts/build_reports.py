"""Regenerate the two Word reports (optional tool: python-docx and Pillow)."""
from pathlib import Path
import re
from docx import Document
from docx.shared import Cm,Pt,Inches
from docx.enum.section import WD_SECTION,WD_ORIENT
from docx.oxml import OxmlElement
from PIL import Image
root=Path(__file__).resolve().parents[1];out=root/'reports';out.mkdir(exist_ok=True)
def plain(s):return s.replace('**','').replace('`','')
def append_markdown(doc,path):
 lines=path.read_text().splitlines();i=0;code=False
 while i<len(lines):
  line=lines[i];i+=1
  if line.startswith('```'):code=not code;continue
  if code:
   run=doc.add_paragraph().add_run(line);run.font.name='Courier New';run.font.size=Pt(9);continue
  if not line.strip():continue
  if line.startswith('!['):continue
  if line.startswith('|'):
   rows=[line]
   while i<len(lines) and lines[i].startswith('|'):rows.append(lines[i]);i+=1
   vals=[[plain(c.strip()) for c in row.strip('|').split('|')] for row in rows if not re.match(r'^\|[\s:|\-]+\|$',row)]
   table=doc.add_table(rows=0,cols=max(map(len,vals)));table.style='Table Grid'
   for number,row in enumerate(vals):
    for cell,text in zip(table.add_row().cells,row):
     cell.text=text
     for paragraph in cell.paragraphs:
      for run in paragraph.runs:run.font.size=Pt(9);run.bold=number==0
    if number==0:table.rows[0]._tr.get_or_add_trPr().append(OxmlElement('w:tblHeader'))
   doc.add_paragraph();continue
  match=re.match(r'^(#+)\s+(.*)',line)
  if match:doc.add_heading(plain(match[2]),level=min(len(match[1]),3))
  elif line.startswith('- '):doc.add_paragraph(plain(line[2:]),style='List Bullet')
  else:doc.add_paragraph(plain(line))
def new_doc(number,title):
 doc=Document();s=doc.sections[0];s.left_margin=Cm(2.2);s.right_margin=Cm(1.5);s.top_margin=s.bottom_margin=Cm(2)
 font=doc.styles['Normal'].font;font.name='Times New Roman';font.size=Pt(12)
 doc.styles['Normal'].paragraph_format.space_after=Pt(6)
 doc.add_heading(f'Лабораторная работа №{number}',0);doc.add_paragraph('Проектирование программного обеспечения',style='Subtitle');doc.add_heading('Финансовое планирование службы такси',1);doc.add_paragraph(title)
 doc.add_paragraph('Студент: ____________________\nГруппа: ____________________\n2026 г.')
 doc.add_paragraph('Основание: PPO-Findirector-Labs.docx и финансовая спецификация ЛР1. Услуга — поездка; правила П-Б1–П-Б10 сохранены. Исходный стартовый архив лектора не предоставлен; расчётное ядро разработано самостоятельно.')
 s.footer.paragraphs[0].add_run(f'ППО • ЛР{number} • Служба такси')
 return doc
def diagrams(doc,files):
 for stem,title in files:
  s=doc.add_section(WD_SECTION.NEW_PAGE);s.orientation=WD_ORIENT.LANDSCAPE;s.page_width=Cm(29.7);s.page_height=Cm(21);s.top_margin=s.bottom_margin=Cm(1.2)
  doc.add_heading(title,1)
  path=root/'docs/diagrams'/f'{stem}.png'
  with Image.open(path) as im:w,h=im.size
  width=min(9.8,5.8*w/h)
  doc.add_picture(str(path),width=Inches(width));doc.add_paragraph(f'Исходник и полноразмерные SVG/PDF: docs/diagrams/{stem}.*',style='Caption')
for number,title,files,images in [
 (2,'Проектирование системы',['docs/lab2.md','docs/design.md','docs/data.md'],[('components','Диаграмма компонентов'),('domain','Диаграмма классов'),('er','Логическая ER-модель')]),
 (3,'Прогноз, сравнение и автоматические проверки',['docs/lab3.md','docs/requirements.md','docs/validation.md','docs/defense.md'],[('domain','Актуальная диаграмма классов ЛР3')])]:
 doc=new_doc(number,title)
 for path in files:doc.add_page_break();append_markdown(doc,root/path)
 if number==3:
  doc.add_page_break();doc.add_heading('Команды запуска вне IDE',1)
  for command in ['dotnet restore TaxiFinance.sln --locked-mode','dotnet build TaxiFinance.sln -c Release --no-restore','dotnet test TaxiFinance.sln -c Release --no-build','dotnet run --project src/TaxiFinance.Console -c Release --no-build -- demo','dotnet run --project src/TaxiFinance.Console -c Release --no-build -- forecast examples/plan-b.json']:
   run=doc.add_paragraph().add_run(command);run.font.name='Courier New';run.font.size=Pt(9)
 diagrams(doc,images)
 name='ЛР2_Такси_Проектирование.docx' if number==2 else 'ЛР3_Такси_Прогноз_и_тесты.docx'
 doc.save(out/name);print(name)
