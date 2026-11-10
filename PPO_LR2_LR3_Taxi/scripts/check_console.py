"""Black-box checks of the compiled Console adapter; no external Python packages.
Run: python3 scripts/check_console.py --dotnet dotnet
"""
from pathlib import Path
import argparse,json,subprocess,tempfile
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--dotnet',default='dotnet');args=parser.parse_args()
dll=root/'src/TaxiFinance.Console/bin/Release/net10.0/TaxiFinance.Console.dll'
checks=[]
def invoke(*command,exit_code=0):
 proc=subprocess.run([args.dotnet,str(dll),*map(str,command),'--json'],cwd=root,capture_output=True,text=True)
 assert proc.returncode==exit_code,(proc.returncode,proc.stdout,proc.stderr)
 return json.loads(proc.stdout)
def ok(name):checks.append(name);print('PASS:',name)
for f,cash,debt,funding in [('plan-a',-10000,150000,70000),('plan-b',65000,75000,0),('plan-c',140000,0,0),('varied-months',38800,0,0)]:
 result=invoke('forecast',root/'examples'/f'{f}.json')
 assert len(result['Months'])==3
 assert result['Summary']=={'TotalProfit':28800 if f=='varied-months' else 90000,'FinalCash':cash,'FinalDebt':debt,'RequiredFinancing':funding}
 ok(f'forecast {f}: monthly count and all summary fields')
result=invoke('compare',root/'examples/plan-a.json',root/'examples/plan-b.json')
assert result['Left']['Summary']['FinalCash']==-10000 and result['Right']['Summary']['FinalCash']==65000
ok('compare two files: independent results')
result=invoke('forecast',root/'examples/invalid-percent.json',exit_code=2)
assert result['Error']['Field']=='Months[2].PaymentPercent';ok('A=101: nonzero exit and exact field')
base=json.loads((root/'examples/plan-b.json').read_text())
with tempfile.TemporaryDirectory(prefix='taxi-finance-cli-') as directory:
 path=Path(directory)/'input.json'
 cases=[]
 data=json.loads(json.dumps(base));data['Months'][0]['Quantity']=0.5;cases.append(('fractional quantity',json.dumps(data)))
 data=json.loads(json.dumps(base));data['Months'][0]['PaymentPercent']=50.5;cases.append(('fractional payment percent',json.dumps(data)))
 data=json.loads(json.dumps(base));data['UnknownField']=1;cases.append(('unknown field',json.dumps(data)))
 data=json.loads(json.dumps(base));del data['Months'];cases.append(('missing months',json.dumps(data)))
 cases.append(('duplicate key',json.dumps(base)[:-1]+',"InitialCash":0}'))
 for name,source in cases:
  path.write_text(source);result=invoke('forecast',path,exit_code=2)
  assert 'Error' in result and result['Error']['Field'];ok(name+': input rejected')
 result=invoke('compare',root/'examples/plan-a.json',root/'examples/plan-a.json',exit_code=2)
 assert result['Error']['Field']=='Plans';ok('same plan comparison rejected')
 result=invoke('forecast',Path(directory)/'missing.json',exit_code=2)
 assert result['Error']['Field']=='InputFile';ok('missing input file rejected')
(root/'docs/test-results/console-checks.json').write_text(json.dumps({'count':len(checks),'passed':checks,'failed':0},ensure_ascii=False,indent=2)+'\n')
print('Console checks passed:',len(checks))
