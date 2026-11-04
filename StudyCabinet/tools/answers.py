# Правильные ответы на вопросы теста и контрольные вопросы лекции — для автотеста (читает базу SQLite)
import json, sqlite3, sys
db, lecture = sys.argv[1], int(sys.argv[2])
c = sqlite3.connect(db)
def opts(col, qid):
    return [{'text': t, 'match': m, 'correct': bool(k)} for t, m, k in c.execute(f'select Text, MatchText, IsCorrect from AnswerOptions where {col}=? order by "Order"', (qid,))]
out = {'test': [], 'control': []}
for qid, text, typ in c.execute('select q.Id, q.Text, q.Type from TestQuestions q join Tests t on t.Id=q.TestId where t.LectureId=?', (lecture,)).fetchall():
    out['test'].append({'id': qid, 'text': text, 'type': typ, 'options': opts('TestQuestionId', qid)})
for qid, text, typ in c.execute('select Id, Text, Type from ControlQuestions where LectureId=?', (lecture,)).fetchall():
    out['control'].append({'id': qid, 'text': text, 'type': typ, 'options': opts('ControlQuestionId', qid)})
print(json.dumps(out, ensure_ascii=False))
