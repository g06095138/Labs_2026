"""Independent arithmetic check of specification expectations; not application tests."""
from decimal import Decimal,ROUND_HALF_UP
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
expected=json.loads((root/'docs/calculation-check.json').read_text())
def calculate(cash,months):
 cash=Decimal(str(cash));debt=Decimal(0);rows=[]
 for n,(q,p,v,f,a) in enumerate(months,1):
  p,v,f=(Decimal(str(x)) for x in (p,v,f))
  if not isinstance(a,int) or not 0<=a<=100:raise ValueError('Month.PaymentPercent: целое 0…100')
  revenue=q*p;variable=q*v;expenses=variable+f
  payment=(revenue*a/100).quantize(Decimal('.01'),rounding=ROUND_HALF_UP)
  income=payment+debt;cash=cash+income-expenses;debt=revenue-payment
  rows.append(dict(month=n,revenue=revenue,variable=variable,margin=revenue-variable,expenses=expenses,profit=revenue-expenses,current_payment=payment,income=income,cash=cash,debt=debt))
 return dict(months=rows,profit=sum(r['profit'] for r in rows),cash=cash,debt=debt,financing=max(Decimal(0),-min(r['cash'] for r in rows)))
base=(500,300,100,70000)
cases={**{f'A{a}':(50000,[(*base,a)]*3) for a in (0,50,100)},
 'no_trips':(50000,[(0,300,100,70000,50)]*3),
 'zero_cash':(75000,[(500,300,100,100000,50)]*3),
 'updated_B':(50000,[(*base,100),(*base,50),(*base,50)]),
 'kopeck':(0,[(1,'0.01',0,0,50)]*3)}
def verify(actual,wanted):
 if isinstance(actual,dict):
  assert set(actual)==set(wanted)
  for k in actual:verify(actual[k],wanted[k])
 elif isinstance(actual,list):
  assert len(actual)==len(wanted)
  for a,b in zip(actual,wanted):verify(a,b)
 else:assert Decimal(str(actual))==Decimal(str(wanted)),(actual,wanted)
text=(root/'docs/requirements.md').read_text()
for name,(cash,months) in cases.items():
 result=calculate(cash,months);verify(result,expected[name]);print('OK:',name)
 # Confirm the monthly acceptance tables agree with the calculated numbers.
 if name!='kopeck':
  for r in result['months']:
   vals=[r[k] for k in ('month','revenue','margin','expenses','profit','current_payment','income','cash','debt')]
   row='| '+' | '.join(str(int(x)) for x in vals)+' |'
   assert row in text,('Missing/mismatched table row',name,row)
try:calculate(50000,[(*base,50),(*base,101),(*base,50)])
except ValueError:print('OK: invalid percentage rejected by arithmetic check')
else:raise AssertionError('Invalid percentage accepted')
assert sum(line.startswith('### ПР') for line in text.splitlines())==8
print('Eight examples present; arithmetic and monthly table rows verified.')
