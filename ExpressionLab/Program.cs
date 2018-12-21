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
            Expression<Func<SomeDerivedClass, object>> test = i => i.Prop;
            var body = (UnaryExpression)test.Body;
            Console.WriteLine(((MemberExpression)body.Operand).Member.ReflectedType);

            var abc = new SomeDerivedClass();
            
            var jj = ((SomeDerivedClass)((SomeClass)abc)).Prop;

            foreach (var item in abc.GetType().GetProperties())
            {
                Console.WriteLine(item.ReflectedType);
            }

            //var b = GetDatas<BaseTable>("A", x => x.Id == 1 && x.Name == "David");

            //等同於以下
            var db = new MyDbContext();

            var a = db.A.Where(x => x.Id == 1 && x.Name == "David").ToList();

            //舊版
            var a1 = GetDatas<BaseTable>("A", x => x.Id == 1 && x.Name == "David");

            //新版
            var b = db.B.Where(x => x.Id == 1).Where(x => x.Name == "David").OrderByDescending(x => x.Name).ToDynamicTableDatas("A", db);
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

    public class SomeClass
    {
        public int Prop { get; set; } = 1;
    }

    public class SomeDerivedClass : SomeClass
    {
        public int Prop { get; set; } = 2;
    }

    public static class DynamicQueryable
    {
        public static List<T> ToDynamicTableDatas<T>(this IQueryable<T> source, string tableName,object dbContext)
        {
            // init
            List<T> finalDatas = new List<T>();
            Expression whereExpression = null;
            List<Expression> orderByExpression = new List<Expression>();
            List<Expression> orderByDescExpression = new List<Expression>();

            #region EF Table Info

            // Get DbSet<T>
            var table = dbContext.GetType().GetProperties().FirstOrDefault(x => x.Name == tableName);            
            var tableInstance = table.GetValue(dbContext);
            if (table == null)
            {
                throw new Exception("No Table");
            }

            // Get Table Type
            var tableType = table.PropertyType.GenericTypeArguments.First();            

            #endregion

            #region init Method

            var whereMethod = typeof(Queryable)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(mi => mi.Name == "Where");

            whereMethod = whereMethod.MakeGenericMethod(new Type[] { tableType });

            var toListMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(mi => mi.Name == "ToList");

            toListMethod = toListMethod.MakeGenericMethod(new Type[] { tableType });

            var orderByDescendingMethod = typeof(Enumerable)
               .GetMethods(BindingFlags.Static | BindingFlags.Public)
               .FirstOrDefault(mi => mi.Name == "OrderByDescending");

            orderByDescendingMethod = orderByDescendingMethod.MakeGenericMethod(new Type[] { tableType , typeof(string) });

            #endregion

            #region scan Expression

            var newParam = Expression.Parameter(tableType, "x");
            GetNextNode(source.Expression);

            
            Expression GetNextNode(Expression node, bool isWhere = false)
            {
                switch (node)
                {
                    case LambdaExpression le:

                        return GetNextNode(le.Body , isWhere);

                    case UnaryExpression ue:

                        return GetNextNode(ue.Operand , isWhere);

                    case ParameterExpression pe:

                        return newParam;

                    case MemberExpression me:
                        
                        var newNode = GetNextNode(me.Expression);

                        return Expression.MakeMemberAccess(newNode, newNode.Type.GetMember(me.Member.Name).First());

                    case BinaryExpression be:

                        return Expression.MakeBinary(node.NodeType, GetNextNode(be.Left), GetNextNode(be.Right));

                    case MethodCallExpression mce:

                        if (isWhere)
                        {
                            List<Expression> arguments = new List<Expression>();
                            foreach (var argument in mce.Arguments)
                            {
                                arguments.Add(GetNextNode(argument));
                            }
                            var instance = GetNextNode(mce.Object);

                            return Expression.Call(instance, mce.Method, arguments);
                        }
                        else
                        {
                            List<Expression> args = new List<Expression>();

                            foreach (var item in mce.Arguments)
                            {
                                switch (item.NodeType)
                                {
                                    case ExpressionType.Quote:

                                        Expression expor = GetNextNode(item, true);

                                        switch (mce.Method.Name.ToLower())
                                        {
                                            case "where":

                                                if (whereExpression == null)
                                                {
                                                    whereExpression = expor;
                                                }
                                                else
                                                {
                                                    whereExpression = Expression.AndAlso(whereExpression, expor);
                                                }

                                                break;
                                            case "orderby":
                                                orderByExpression.Add(expor);
                                                break;

                                            case "orderbydescending":
                                                orderByDescExpression.Add(expor);
                                                break;

                                            default:
                                                break;
                                        }

                                        break;

                                    case ExpressionType.Call:

                                        GetNextNode(item);

                                        break;

                                    case ExpressionType.Constant:

                                        return item;

                                    default:
                                        break;
                                }
                            }
                        }

                        return node;

                    default:

                        return node;
                }
            }

            #endregion

            #region invoke

            if (whereExpression != null)
            {
                var whereLambda = Expression.Lambda(Expression.GetFuncType(new Type[] { tableType, typeof(bool) }), whereExpression, newParam);

                var whereAfter = whereMethod.Invoke(tableInstance, new object[] { tableInstance, whereLambda });

                // 取得所執行的SQL
                var ggg = whereAfter.GetType().GetProperties().FirstOrDefault(x=>x.Name == "Sql")?.GetValue(whereAfter);

                var orderByDescLambda = Expression.Lambda(Expression.GetFuncType(new Type[] { tableType, orderByDescExpression.First().Type }), orderByDescExpression.First(), newParam);

                var orderByDescAfter = orderByDescendingMethod.Invoke(tableInstance, new object[] { whereAfter, orderByDescLambda.Compile() });
                
                var enumerableDatas = toListMethod.Invoke(null, new object[] { orderByDescAfter }) as IEnumerable<object>;
            }
            else
            {
                var orderByDescLambda = Expression.Lambda(Expression.GetFuncType(new Type[] { tableType, typeof(int) }), orderByDescExpression.First(), newParam);

                var orderByDescAfter = orderByDescendingMethod.Invoke(tableInstance, new object[] { tableInstance , orderByDescLambda.Compile() });

                var enumerableDatas = toListMethod.Invoke(null, new object[] { orderByDescAfter }) as IEnumerable<object>;
            }
            
            #endregion

            //TODO  Object to T

            return finalDatas;
        }
    }
}
