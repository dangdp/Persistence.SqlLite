using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.SqlLite
{
    public static class SqlExtension
    {
        internal static string PreventSqlInjection(this string value)
        {
            return value.Replace("'", "''");
        }

        private static Dictionary<string, Action<Expression, SqlStringBuilder>> MappingExpression = new Dictionary<string, Action<Expression, SqlStringBuilder>>();
        private static Dictionary<ExpressionType, string> MappingCompare = new Dictionary<ExpressionType, string>() {
            { ExpressionType.Equal, "=" },
            { ExpressionType.GreaterThanOrEqual, ">=" },
            { ExpressionType.GreaterThan, ">" },
            { ExpressionType.LessThanOrEqual, "<=" },
            { ExpressionType.LessThan, "<" },
            { ExpressionType.NotEqual, "!=" }
        };


        private const string Const_GetDate = "GETDATE()";
        private const string Const_GetUtcDate = "GETUTCDATE()";

        private static void InitializeExpression()
        {
            if (MappingExpression.Count > 0) return;
            lock (IsLocked)
            {
                if (MappingExpression.Count > 0) return;

                MappingExpression.Add("ConstantExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((ConstantExpression)expression, sb);
                });

                MappingExpression.Add("TypedConstantExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    var constantExression = expression as ConstantExpression;
                    if (constantExression.Value == null)
                    {
                        sb.Append("NULL");
                        return;
                    }
                });

                MappingExpression.Add("InstanceMethodCallExpressionN", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });
                MappingExpression.Add("InstanceMethodCallExpression0", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });
                MappingExpression.Add("InstanceMethodCallExpression1", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });
                MappingExpression.Add("InstanceMethodCallExpression()", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });

                MappingExpression.Add("MethodCallExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });

                MappingExpression.Add("GreaterThanOrEqual", (Expression expression, SqlStringBuilder sb) =>
                {
                    WriteValue((MethodCallExpression)expression, sb);
                });

                MappingExpression.Add("PropertyExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    if (expression.NodeType == ExpressionType.MemberAccess)
                    {
                        var member = (expression as MemberExpression);
                        var exressionName = member.Expression.GetType().Name;
                        if (exressionName == "TypedParameterExpression")
                        {
                            sb.Append($"[{member.Member.Name}]");
                            return;
                        }

                        if (exressionName == "UnaryExpression")
                        {
                            sb.Append($"[{member.Member.Name}]");
                            return;
                        }

                        if (exressionName == "FieldExpression")
                        {
                            var parentMemberExpression = member.Expression as MemberExpression;
                            var constantParentExpression = parentMemberExpression.Expression as ConstantExpression;
                            object valueParent = null;
                            if (constantParentExpression == null)
                            {
                                valueParent = Expression.Lambda(member.Expression).Compile().DynamicInvoke();
                            }
                            else
                            {
                                var fieldParent = constantParentExpression.Value.GetType().GetField(parentMemberExpression.Member.Name);
                                valueParent = fieldParent != null ? fieldParent.GetValue(constantParentExpression.Value) : constantParentExpression.Value;
                            }

                            var value = (member.Member as System.Reflection.PropertyInfo).GetValue(valueParent);
                            if (value == null && sb.Current != null && sb.Current.Content == " = ")
                            {
                                sb.Current.Content = " IS ";
                            }
                            var valueSql = GetSqlValueAsCompare(value);
                            sb.Append(valueSql);
                            return;
                        }

                        if (member.Expression == null)
                        {
                            if (expression.ToString() == "DateTime.Now")
                            {
                                sb.Append(Const_GetDate);
                                return;
                            }

                            if (expression.ToString() == "DateTime.UtcNow")
                            {
                                sb.Append(Const_GetUtcDate);
                                return;
                            }
                        }
                        if (exressionName == "PropertyExpression")
                        {
                            var value = Expression.Lambda(expression).Compile().DynamicInvoke();
                            var valueSql = GetSqlValueAsCompare(value);
                            sb.Append(valueSql);
                            return;
                        }
                        throw new Exception($"FieldExpression: Not Support Expression {expression.ToString()}");
                    }
                });

                MappingExpression.Add("UnaryExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    var unaryExpression = (expression as UnaryExpression);

                    var member = unaryExpression.Operand as MemberExpression;
                    if (member != null)
                    {

                        if (member.Expression == null)
                        {
                            if (member.ToString() == "DateTime.Now")
                            {
                                sb.Append(Const_GetDate);
                                return;
                            }

                            if (member.ToString() == "DateTime.UtcNow")
                            {
                                sb.Append(Const_GetUtcDate);
                                return;
                            }

                            throw new Exception("Not Support This Expression");
                        }
                        var exressionName = member.Expression.GetType().Name;

                        var isHaveValue = false;

                        if (exressionName == "TypedParameterExpression")
                        {
                            sb.Append($"[{member.Member.Name}]");
                            isHaveValue = true;
                        }

                        if (unaryExpression.NodeType == ExpressionType.Not)
                        {
                            sb.Append($" = 0");
                            isHaveValue = true;
                        }

                        if (isHaveValue)
                        {
                            return;
                        }

                        if (unaryExpression.NodeType == ExpressionType.Convert)
                        {
                            var value = Expression.Lambda(unaryExpression).Compile().DynamicInvoke();

                            if (value != null)
                            {
                                var sqlValue = GetSqlValueAsCompare(value);
                                sb.Append(sqlValue);
                                return;
                            }
                        }

                        return;
                    }

                    if (unaryExpression.NodeType == ExpressionType.Convert)
                    {
                        var value = Expression.Lambda(unaryExpression).Compile().DynamicInvoke();

                        if (value != null)
                        {
                            var sqlValue = GetSqlValueAsCompare(value);
                            sb.Append(sqlValue);
                            return;
                        }
                    }

                    if (unaryExpression.Operand.NodeType == ExpressionType.Call)
                    {
                        var isNot = unaryExpression.NodeType == ExpressionType.Not || unaryExpression.NodeType == ExpressionType.NotEqual;
                        WriteValue(unaryExpression.Operand as MethodCallExpression, sb, isNot);
                        return;
                    }

                    throw new Exception($"FieldExpression: Member Expression is null + {expression.ToString()}");
                });

                MappingExpression.Add("FieldExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    var memberExpression = (expression as MemberExpression);
                    if (memberExpression == null)
                    {
                        throw new Exception($"FieldExpression: Member Expression is null + {expression.ToString()}");
                    }

                    var valueObject = Expression.Lambda(memberExpression.Expression).Compile().DynamicInvoke();
                    var fields = valueObject.GetType().GetFields();

                    var valueProperty = fields.FirstOrDefault(m => m.Name == memberExpression.Member.Name);

                    if (valueProperty != null)
                    {
                        var value = valueProperty.GetValue(valueObject);
                        var valueSql = GetSqlValueAsCompare(value);
                        sb.Append(valueSql);
                        return;
                    }

                    throw new Exception($"FieldExpression: Not Support + {expression.ToString()}");
                });

                MappingExpression.Add("MethodBinaryExpression", (Expression expression, SqlStringBuilder sb) =>
                {
                    var body = expression as BinaryExpression;
                    var leftMember = body.Left as MemberExpression;
                    var bodyLeftExpressionName = body.Left.GetType().Name;
                    var rightMember = body.Right as MemberExpression;
                    var bodyRightExpressionName = body.Right.GetType().Name;
                    var isConstantExpression = false;
                    if (bodyLeftExpressionName == "TypedConstantExpression" || bodyRightExpressionName == "TypedConstantExpression")
                    {
                        isConstantExpression = true;
                    }



                    if (MappingExpression.ContainsKey(bodyLeftExpressionName))
                    {
                        MappingExpression[bodyLeftExpressionName](body.Left, sb);
                    }
                    else
                    {
                        throw new Exception($"Not Support: {bodyLeftExpressionName} - {leftMember.ToString()}");
                    }

                    if (isConstantExpression)
                    {
                        if (body.NodeType == ExpressionType.NotEqual)
                        {
                            sb.Append($" IS NOT ");
                        }

                        if (body.NodeType == ExpressionType.Equal)
                        {
                            sb.Append($" IS ");
                        }
                    }
                    else
                    {
                        if (MappingCompare.ContainsKey(body.NodeType))
                        {
                            sb.Append($" {MappingCompare[body.NodeType]} ");
                        }
                        else
                        {
                            throw new Exception($"Not Support: {body.NodeType}");
                        }
                    }


                    if (MappingExpression.ContainsKey(bodyRightExpressionName))
                    {
                        MappingExpression[bodyRightExpressionName](body.Right, sb);
                    }
                    else
                    {
                        throw new Exception($"Not Support: {bodyLeftExpressionName} - {leftMember.ToString()}");
                    }
                });
            }
        }

        public static string ToSql<T>(this Expression<Func<T, bool>> predicate)
        {
            Initialize();
            InitializeExpression();
            var bodyType = (predicate.Body as Expression).GetType().Name;
            if (predicate.Parameters.Count > 1)
            {
                throw new Exception("Not Support Multiple Parameter");
            }
            var body = predicate.Body as BinaryExpression;
            var sb = new SqlStringBuilder();

            if (bodyType == "ConstantExpression")
            {
                var constantExpression = predicate.Body as ConstantExpression;
                if (constantExpression.Type.FullName == "System.Boolean")
                {
                    var result = Convert.ToBoolean(predicate.Body.ToString());

                    if (result)
                    {
                        return " 1 = 1 ";
                    }
                    else
                    {
                        return "1 = 2";
                    }
                }
            }

            if (bodyType == "PropertyExpression")
            {
                PrintBooleanExpression(predicate.Body, sb);
                return sb.ToString();
            }

            if (bodyType == "MethodCallExpression2")
            {
                MappingExpression["MethodCallExpression"](predicate.Body, sb);
                return sb.ToString();
            }


            if (MappingExpression.ContainsKey(bodyType))
            {
                MappingExpression[bodyType](predicate.Body, sb);
                return sb.ToString();
            }

            GetCondition(body, sb);

            var condition = sb.ToString();
            return condition;
        }

        private static void PrintBooleanExpression(Expression body, SqlStringBuilder sb)
        {
            MappingExpression["PropertyExpression"](body, sb);
            var type = ((body as MemberExpression).Member as PropertyInfo).PropertyType.FullName;
            if (type == "System.Boolean")
            {
                sb.Append(" = 1");
            }
        }

        public static string GetFieldName<T>(this Expression<Func<T, object>> predicate)
        {

            var body = predicate.Body as MemberExpression;
            if (body == null)
            {
                body = ((predicate.Body as UnaryExpression).Operand as MemberExpression);
            }

            if (body != null)
            {
                return body.Member.Name;
            }
            else
            {
                throw new Exception("Please check your syntax. Check at: ...");
            }
        }

        public static string GetFieldName<T,T1>(this Expression<Func<T, T1>> predicate)
        {

            var body = predicate.Body as MemberExpression;
            if (body == null)
            {
                body = ((predicate.Body as UnaryExpression).Operand as MemberExpression);
            }

            if (body != null)
            {
                return body.Member.Name;
            }
            else
            {
                throw new Exception("Please check your syntax. Check at: ...");
            }
        }

        private const string Const_Parentheses_Start = "(";
        private const string Const_Parentheses_End = ")";
        private const string Const_Compare_AND = " AND ";
        private const string Const_Compare_OR = " OR ";

        private static void GetCondition(Expression expression, SqlStringBuilder sb, ExpressionType? parentNodeType = null)
        {
            var body = expression as BinaryExpression;
            var expressionName = expression.GetType().Name;
            if (MappingExpression.ContainsKey(expressionName))
            {
                MappingExpression[expressionName](expression, sb);
                return;
            }



            if (body == null)
            {
                var methodBody = expression as MethodCallExpression;
                WriteValue(methodBody, sb);
                return;
            }

            if (body.NodeType == ExpressionType.AndAlso || body.NodeType == ExpressionType.OrElse)
            {
                if (parentNodeType != null && body.NodeType != parentNodeType.Value)
                {
                    sb.Append(Const_Parentheses_Start);
                }

                if (body.Left.NodeType == ExpressionType.MemberAccess && body.Left.Type.FullName == "System.Boolean")
                {
                    PrintBooleanExpression(body.Left, sb);
                }
                else
                {
                    GetCondition(body.Left, sb, body.NodeType);
                }
                if (body.NodeType == ExpressionType.AndAlso)
                {
                    sb.Append(Const_Compare_AND);
                }

                if (body.NodeType == ExpressionType.OrElse)
                {
                    sb.Append(Const_Compare_OR);
                }

                if (body.Right.NodeType == ExpressionType.MemberAccess && body.Right.Type.FullName == "System.Boolean")
                {
                    PrintBooleanExpression(body.Right, sb);
                }
                else
                {
                    GetCondition(body.Right, sb, body.NodeType);
                }

                if (parentNodeType != null && body.NodeType != parentNodeType.Value)
                {
                    sb.Append(Const_Parentheses_End);
                }

                return;
            }

            if (
                    body.NodeType == ExpressionType.GreaterThan
                    || body.NodeType == ExpressionType.GreaterThanOrEqual
                    || body.NodeType == ExpressionType.LessThan
                    || body.NodeType == ExpressionType.Not
                    || body.NodeType == ExpressionType.NotEqual
                    || body.NodeType == ExpressionType.Equal
                    || body.Left.NodeType == ExpressionType.Constant
                    || body.Right.NodeType == ExpressionType.Constant
                    || body.NodeType == ExpressionType.LessThanOrEqual)
            {
                MappingExpression["MethodBinaryExpression"](expression, sb);
            }

            //if (body != null && body.Left.GetType().Name == "UnaryExpression" || body.Left.GetType().Name == "UnaryExpression")
            //{
            //    BinaryExpression binaryExpression = (BinaryExpression)body;
            //    var value = ((((((UnaryExpression)binaryExpression.Right).Operand as MemberExpression).Expression) as MemberExpression).Expression as ConstantExpression).Value;
            //    var convert = (UnaryExpression)binaryExpression.Left;
            //    var propertyExpression = (MemberExpression)convert.Operand;
            //    var property = (PropertyInfo)propertyExpression.Member;
            //    Enum enumValue = (Enum)Enum.ToObject(property.PropertyType, value);
            //    return;
            //}

        }

        private static Dictionary<string, bool> MappingOriginTypes = new Dictionary<string, bool>();

        private static object IsLocked = new object();

        private static void Initialize()
        {
            if (MappingOriginTypes.Count == 0)
            {
                lock (IsLocked)
                {
                    if (MappingOriginTypes.Count == 0)
                    {
                        MappingOriginTypes.Add("System.String", true);
                        MappingOriginTypes.Add("System.DateTime", true);
                        MappingOriginTypes.Add("System.Boolean", true);
                        MappingOriginTypes.Add("System.Int", true);
                        MappingOriginTypes.Add("System.Integer", true);
                        MappingOriginTypes.Add("System.Byte", true);
                        MappingOriginTypes.Add("System.SByte", true);
                        MappingOriginTypes.Add("System.UInt16", true);
                        MappingOriginTypes.Add("System.UInt32", true);
                        MappingOriginTypes.Add("System.UInt64", true);
                        MappingOriginTypes.Add("System.Int16", true);
                        MappingOriginTypes.Add("System.Int32", true);
                        MappingOriginTypes.Add("System.Int64", true);
                        MappingOriginTypes.Add("System.Decimal", true);
                        MappingOriginTypes.Add("System.Double", true);
                        MappingOriginTypes.Add("System.Float", true);
                        MappingOriginTypes.Add("System.Single", true);
                        MappingOriginTypes.Add("System.Long", true);
                        MappingOriginTypes.Add("System.Guid", true);
                    }
                }
            }
        }

        private static void WriteValue(ConstantExpression constantExpression, SqlStringBuilder sb)
        {
            Initialize();
            if (constantExpression.Value == null)
            {
                sb.Append("NULL");
                return;
            }

            if (constantExpression.Value != null && MappingOriginTypes.ContainsKey(constantExpression.Value.GetType().FullName))
            {
                var valueSql = GetSqlValueAsCompare(constantExpression.Value);
                sb.Append(valueSql);
                return;
            }

            var fields = constantExpression.Value.GetType().GetFields().ToList();
            var value = fields.Count == 1 ? fields.FirstOrDefault().GetValue(constantExpression.Value) : constantExpression.Value;

            if (value == null)
            {
                sb.Append("NULL");
            }
            else
            {
                var valueSql = GetSqlValueAsCompare(value);
                sb.Append(valueSql);
            }
        }

        private static object ReadValue(MemberExpression expression)
        {
            if (expression.Expression is ConstantExpression)
            {
                return (((ConstantExpression)expression.Expression).Value)
                        .GetType()
                        .GetField(expression.Member.Name)
                        .GetValue(((ConstantExpression)expression.Expression).Value);
            }
            if (expression.Expression is MemberExpression) return ReadValue((MemberExpression)expression.Expression);

            throw new NotSupportedException(expression.ToString());
        }

        private static string CheckRunTimeType(object value)
        {
            if (value is Guid)
            {
                return PersistenceContext.MappingValueTypes[typeof(Guid).FullName];
            }
            if (value is String)
            {
                return PersistenceContext.MappingValueTypes[typeof(String).FullName];
            }

            if (value is DateTime)
            {
                return PersistenceContext.MappingValueTypes[typeof(DateTime).FullName];
            }

            if (value is int)
            {
                return PersistenceContext.MappingValueTypes[typeof(int).FullName];
            }

            if (value is long)
            {
                return PersistenceContext.MappingValueTypes[typeof(int).FullName];
            }

            if (value is Enum)
            {
                return "ENUM";
            }

            if (value is bool)
            {
                return PersistenceContext.MappingValueTypes[typeof(bool).FullName];
            }

            if (value is uint)
            {
                return PersistenceContext.MappingValueTypes[typeof(bool).FullName];
            }

            if (value == null)
            {
                throw new Exception("NOT SUPPORT TYPE");
            }

            throw new Exception("NOT SUPPORT TYPE: " + value.GetType().FullName);
        }

        private static void WriteValue(MethodCallExpression methodExpression, SqlStringBuilder sb, bool? isNot = false)
        {
            var method = methodExpression.Method;
            if (method.Name == "Contains")
            {
                var arguments = methodExpression.Arguments;

                if (arguments.Count == 1)
                {
                    var argument = arguments.FirstOrDefault() as MemberExpression;
                    if (argument == null && (arguments.FirstOrDefault() as UnaryExpression) != null)
                    {
                        argument = (arguments.FirstOrDefault() as UnaryExpression).Operand as MemberExpression;
                    }
                    
                    var memberExpression = methodExpression.Object as MemberExpression;

                    if (method.DeclaringType.IsGenericType == false)
                    {
                        ConstantExpression constantExpression = null;
                        if (argument == null && arguments.FirstOrDefault().Type.FullName == "System.String")
                        {
                            constantExpression = arguments.FirstOrDefault() as ConstantExpression;
                        } else
                        {
                            constantExpression = argument.Expression as ConstantExpression;
                        }
                        if (constantExpression != null)
                        {
                            string valueString = null;
                            if (argument != null)
                            {
                                var field = constantExpression.Value.GetType().GetField(argument.Member.Name);
                                var valueObject = field != null ? field.GetValue(constantExpression.Value) : constantExpression.Value;

                                valueString = GetSqlValue(valueObject);
                            } else
                            {
                                valueString = constantExpression.Value?.ToString();
                            }
                           

                            sb.Append($"[{memberExpression.Member.Name}] LIKE N'%{valueString}%'");
                        }
                        else
                        {
                            constantExpression = memberExpression.Expression as ConstantExpression;
                            var field = constantExpression.Value.GetType().GetField(memberExpression.Member.Name);
                            var valueObject = field != null ? field.GetValue(constantExpression.Value) : constantExpression.Value;
                            var valueString = GetSqlValue(valueObject);
                            sb.Append($"N'{valueString}' LIKE N'%' + [{argument.Member.Name}] + '%'");
                        }
                    }
                    else
                    {
                        var nameColumn = argument.Member.Name;

                        if (Nullable.GetUnderlyingType(argument.Member.DeclaringType) != null)
                        {
                            var expressionName = argument.Expression.GetType().Name;
                            if (MappingExpression.ContainsKey(expressionName))
                            {
                                MappingExpression[expressionName](argument.Expression, sb);
                            }
                            else
                            {
                                throw new Exception("Not Found");
                            }
                        }
                        else
                        {
                            sb.Append("[" + nameColumn + "]");
                        }


                        if (isNot == true)
                        {
                            sb.Append(" NOT IN ");
                        }
                        else
                        {
                            sb.Append(" IN ");
                        }



                        var constantExpression = memberExpression.Expression as ConstantExpression;
                        object value;
                        if (constantExpression == null)
                        {
                            var model = Expression.Lambda(memberExpression.Expression).Compile().DynamicInvoke();
                            var propertyInfo = memberExpression.Member as PropertyInfo;
                            var fieldInfo = memberExpression.Member as FieldInfo;
                            if (propertyInfo != null)
                            {
                                value = propertyInfo.GetValue(model);
                            }
                            else
                            {
                                value = fieldInfo.GetValue(model);
                            }

                        }
                        else
                        {
                            var field = constantExpression.Value.GetType().GetField(memberExpression.Member.Name);
                            value = field != null ? field.GetValue(constantExpression.Value) : constantExpression.Value;
                        }



                        var values = (IEnumerable) value;
                        var conditions = new List<string>();
                        string referenceType = null;
                        //With dynamic type we cannot use first or default
                        foreach (var valueDynamic in values)
                        {
                            referenceType = CheckRunTimeType((object)valueDynamic);
                            if (!string.IsNullOrEmpty(referenceType))
                            {
                                //With dynamic type we cannot use first or default
                                break;
                            }

                        }
                        if (string.IsNullOrEmpty(referenceType))
                        {
                            throw new Exception("Not support search empty array");
                        }
                        foreach (var valueDynamic in values)
                        {
                            var valueObject = (object)valueDynamic;
                            if (valueObject == null)
                            {
                                throw new Exception("Not support null value in generic type in method: " + method.Name);
                            }

                            string valueString = GetSqlValueAsCompare(valueObject, referenceType);

                            conditions.Add(valueString);
                        }
                        sb.Append(Const_Parentheses_Start + string.Join(", ", conditions) + Const_Parentheses_End);
                    }
                    return;
                }

                if (arguments.Count == 2)
                {
                    var memberArgument = arguments.FirstOrDefault(x => x.Type.IsArray == false && x.Type.Namespace != "System.Collections.Generic");
                    if (memberArgument == null)
                    {
                        throw new Exception("Not Support");
                    }

                    var memberExpression = memberArgument as MemberExpression;

                    var nameColumn = memberExpression.Member.Name;

                    if (Nullable.GetUnderlyingType(memberExpression.Member.DeclaringType) != null)
                    {
                        var expressionName = memberExpression.Expression.GetType().Name;
                        if (MappingExpression.ContainsKey(expressionName))
                        {
                            MappingExpression[expressionName](memberExpression.Expression, sb);
                        }
                        else
                        {
                            throw new Exception("Not Found");
                        }
                    }
                    else
                    {
                        sb.Append("[" + nameColumn + "]");
                    }


                    if (isNot == true)
                    {
                        sb.Append(" NOT IN ");
                    }
                    else
                    {
                        sb.Append(" IN ");
                    }
                    var arrayExpress = arguments.FirstOrDefault(x => x.Type.IsArray == true || x.Type.Namespace == "System.Collections.Generic");
                    var expression = arrayExpress as MemberExpression;
                    var model = Expression.Lambda(arrayExpress).Compile().DynamicInvoke();

                    var values = (IEnumerable)model;
                    var conditions = new List<string>();
                    string referenceType = null;
                    //With dynamic type we cannot use first or default
                    foreach (var valueDynamic in values)
                    {
                        referenceType = CheckRunTimeType((object)valueDynamic);
                        if (!string.IsNullOrEmpty(referenceType))
                        {
                            //With dynamic type we cannot use first or default
                            break;
                        }

                    }
                    if (string.IsNullOrEmpty(referenceType))
                    {
                        throw new Exception("Not support search empty array");
                    }
                    foreach (var valueDynamic in values)
                    {
                        var valueObject = (object)valueDynamic;
                        if (valueObject == null)
                        {
                            throw new Exception("Not support null value in generic type in method: " + method.Name);
                        }

                        string valueString = GetSqlValueAsCompare(valueObject, referenceType);

                        conditions.Add(valueString);
                    }
                    sb.Append(Const_Parentheses_Start + string.Join(", ", conditions) + Const_Parentheses_End);
                    return;
                }

                throw new Exception("Only support one field in method: " + method.Name);
            }

            if (method.Name == "ToString")
            {
                var arguments = methodExpression.Arguments;
                var constantExpression = methodExpression.Object as ConstantExpression;
                if (constantExpression == null)
                {
                    var expressionName = methodExpression.Object.GetType().Name;
                    if (MappingExpression.ContainsKey(expressionName))
                    {
                        MappingExpression[expressionName](methodExpression.Object, sb);
                        return;
                    }

                    throw new Exception("Not support this method");
                }
                var value = constantExpression.Value;
                var sqlValue = GetSqlValueAsCompare(value);

                sb.Append(sqlValue);

                return;
            }

            throw new Exception("Not Support Method: " + method.Name);
        }

        private static Dictionary<string, Func<object, string>> MappingSqlFields = new Dictionary<string, Func<object, string>>()
        {
            { "NUMBER", (valueObject) => { return valueObject.ToString(); } },
            { "ENUM", (valueObject) => { return "'" + valueObject.ToString() + "'"; } },
            { "DATETIME", (valueObject) => { return "'" + Convert.ToDateTime(valueObject).ToString("yyyy-MM-dd HH:mm:ss.fff") + "'"; } },
            { "BIT", (valueObject) => { return Convert.ToInt32(valueObject).ToString(); } },
            { "UNIQUEIDENTIFIER", (valueObject) => { return "'" + valueObject.ToString() + "'"; } }
        };
        private const string Const_Comas = "'";
        private static string GetSqlValueAsCompare(object valueObject, string referenceType = null)
        {
            if (valueObject == null)
            {
                return "NULL";
            }

            var originalType = referenceType ?? CheckRunTimeType(valueObject);

            string valueString;

            if (MappingSqlFields.ContainsKey(originalType))
            {
                return MappingSqlFields[originalType](valueObject);
            }

            valueString = string.Concat(Const_Comas, valueObject.ToString().PreventSqlInjection(), Const_Comas);

            return valueString;
        }

        private static string GetSqlValue(object valueObject)
        {
            var originalType = CheckRunTimeType(valueObject);
            string valueString;
            switch (originalType)
            {
                case "NUMBER":
                    {
                        valueString = valueObject.ToString();
                        break;
                    }
                case "DATETIME":
                    {
                        valueString = Convert.ToDateTime(valueObject).ToString("yyyy-MM-dd HH:mm:ss.fff");
                        break;
                    }
                case "BIT":
                    {
                        valueString = Convert.ToInt32(valueObject).ToString();
                        break;
                    }
                case "UNIQUEIDENTIFIER":
                    {
                        valueString = valueObject.ToString();
                        break;
                    }
                default:
                    {
                        valueString = valueObject.ToString().PreventSqlInjection();
                        break;
                    }
            }

            return valueString;
        }
    }
}
