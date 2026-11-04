# То же, что answers.py, но для SQL Server
import json, sys, pymssql
lecture = int(sys.argv[1])
c = pymssql.connect(server='localhost', port=1433, user='sa', password='Str0ng!Passw0rd', database='StudyCabinet').cursor()
def opts(col, qid):
    c.execute(f'select Text, MatchText, IsCorrect from AnswerOptions where {col}=%s order by [Order]', (qid,))
    return [{'text': t, 'match': m, 'correct': bool(k)} for t, m, k in c.fetchall()]
out = {'test': [], 'control': []}
c.execute('select q.Id, q.Text, q.Type from TestQuestions q join Tests t on t.Id=q.TestId where t.LectureId=%s', (lecture,))
for qid, text, typ in c.fetchall(): out['test'].append({'id': qid, 'text': text, 'type': typ, 'options': opts('TestQuestionId', qid)})
c.execute('select Id, Text, Type from ControlQuestions where LectureId=%s', (lecture,))
for qid, text, typ in c.fetchall(): out['control'].append({'id': qid, 'text': text, 'type': typ, 'options': opts('ControlQuestionId', qid)})
print(json.dumps(out, ensure_ascii=False))
