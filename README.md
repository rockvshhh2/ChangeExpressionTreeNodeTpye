# ChangeExpressionTreeNodeTpye
動態改變先前使用DBContext所下的Table目標
// 以下三種的結果相同

//原始
var db = new MyDbContext();

var a = db.A.Where(x => x.Id == 1 && x.Name == "David").ToList();

//舊版
var a1 = GetDatas<BaseTable>("A", x => x.Id == 1 && x.Name == "David");

//新版
var b = db.B.Where(x => x.Id == 1).Where(x => x.Name == "David").OrderByDescending(x => x.Name).ToDynamicTableDatas("A", db);
