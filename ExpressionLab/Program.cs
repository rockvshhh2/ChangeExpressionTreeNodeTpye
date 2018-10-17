using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ExpressionLab
{
    class Program
    {
        static void Main(string[] args)
        {
            var b = GetDatas<BaseTable>("A", x => x.Id == 1 && x.Name == "David");

            //等同於以下
            //var db = new MyDbContext();
            //var b = db.B.Where(x => x.Id == 1 && x.Name == "David").ToList();
        }

        class BaseTable
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        static List<TFrom> GetDatas<TFrom>(string tableName,Expression<Func<TFrom, bool>> expr = null)
            where TFrom : new()
        {
            // DB Context
            var db = new MyDbContext();

            // BaseClass
            List<TFrom> datas = new List<TFrom>();

            // Get Table
            var table = db.GetType().GetProperties().FirstOrDefault(x => x.Name == tableName);
            var tableInstance = table.GetValue(db);

            if (table == null)
            {
                throw new Exception("No Table");
            }

            // Get Table Type
            var tableType = table.PropertyType.GenericTypeArguments.First();

            var toListMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(mi => mi.Name == "ToList");

            toListMethod = toListMethod.MakeGenericMethod(new Type[] { tableType });

            var toListDatas = new List<object>();

            if (expr != null)
            {
                var oldParam = expr.Parameters[0];
                var newParam = Expression.Parameter(tableType, oldParam.Name);
                Expression newBody = GetNextNode(expr.Body);

                var whereLambda = Expression.Lambda(Expression.GetFuncType(new Type[] { tableType, typeof(bool) }), newBody, newParam);

                var whereMethod = typeof(Queryable)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(mi => mi.Name == "Where");

                whereMethod = whereMethod.MakeGenericMethod(new Type[] { tableType });

                var whereAfter = whereMethod.Invoke(tableInstance, new object[] { tableInstance, whereLambda });

                var enumerableDatas = toListMethod.Invoke(null, new object[] { whereAfter }) as IEnumerable<object>;
                toListDatas = enumerableDatas.ToList();

                Expression GetNextNode(Expression node)
                {

                    switch (node)
                    {
                        case ParameterExpression pe:

                            return newParam;

                        case MemberExpression me:

                            var newNode = GetNextNode(me.Expression);

                            return Expression.MakeMemberAccess(newNode, newNode.Type.GetMember(me.Member.Name).First());

                        case BinaryExpression be:

                            return Expression.MakeBinary(node.NodeType, GetNextNode(be.Left), GetNextNode(be.Right));

                        default:

                            return node;
                    }
                }
            }
            else
            {
                var enumerableDatas = toListMethod.Invoke(null, new object[] { tableInstance }) as IEnumerable<object>;
                toListDatas = enumerableDatas.ToList();
            }


            foreach (var item in toListDatas)
            {
                var data = new TFrom();

                var dbCols = item.GetType().GetProperties();
                var dataCols = data.GetType().GetProperties();

                foreach (var proper in dbCols)
                {
                    var dataCol = dataCols.FirstOrDefault(x => x.Name == proper.Name);

                    if (dataCol != null)
                    {
                        dataCol.SetValue(data, proper.GetValue(item));
                    }
                }

                datas.Add(data);
            }
             
            return datas;
        }
    }
}
