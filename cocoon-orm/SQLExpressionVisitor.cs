﻿using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Text;

namespace Cocoon.ORM
{
    internal class SQLExpressionTranslator : ExpressionVisitor
    {

        private StringBuilder whereBuilder;
        private DbCommand cmd;
        private CocoonORM orm;
        private string tableObjectName;

        public SQLExpressionTranslator()
        {

        }

        public string GenerateSQLExpression(CocoonORM orm, DbCommand cmd, Expression node, string tableObjectName)
        {

            this.cmd = cmd;
            this.orm = orm;
            this.tableObjectName = tableObjectName;

            whereBuilder = new StringBuilder();

            Visit(node);

            return whereBuilder.ToString();

        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            whereBuilder.Append("(");

            Visit(node.Left);

            if (node.NodeType == ExpressionType.And || node.NodeType == ExpressionType.AndAlso)
                whereBuilder.Append(" and ");
            else if (node.NodeType == ExpressionType.Or || node.NodeType == ExpressionType.OrElse)
                whereBuilder.Append(" or ");
            else if (node.NodeType == ExpressionType.LessThan)
                whereBuilder.Append(" < ");
            else if (node.NodeType == ExpressionType.LessThanOrEqual)
                whereBuilder.Append(" <= ");
            else if (node.NodeType == ExpressionType.GreaterThan)
                whereBuilder.Append(" > ");
            else if (node.NodeType == ExpressionType.GreaterThanOrEqual)
                whereBuilder.Append(" >= ");
            else if (node.NodeType == ExpressionType.Equal)
                if (isConstantNull(node.Right))
                    whereBuilder.Append(" is ");
                else
                    whereBuilder.Append(" = ");
            else if (node.NodeType == ExpressionType.NotEqual)
                if (isConstantNull(node.Right))
                    whereBuilder.Append(" is not ");
                else
                    whereBuilder.Append(" <> ");
            else
                throw new NotSupportedException(string.Format("Binary operator '{0}' not supported", node.NodeType));
            
            Visit(node.Right);

            whereBuilder.Append(")");

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
                whereBuilder.Append(string.Format("{0}.{1}", tableObjectName, orm.Platform.getObjectName(node.Member)));
            else
                whereBuilder.Append(orm.Platform.addWhereParam(cmd, getExpressionValue(node)));

            return node;
            //throw new NotSupportedException(string.Format("The member '{0}' is not supported", node.Member.Name));

        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {

            if (node.Method.Name == "StartsWith")
                addLikeParam(node, getExpressionValue(node.Arguments[0]) + "%");
            else if (node.Method.Name == "EndsWith")
                addLikeParam(node, "%" + getExpressionValue(node.Arguments[0]));
            else if (node.Method.Name == "Contains")
                addLikeParam(node, "%" + getExpressionValue(node.Arguments[0]) + "%");
            else
                whereBuilder.Append(orm.Platform.addWhereParam(cmd, Expression.Lambda(node).Compile().DynamicInvoke()));
            //throw new NotSupportedException(string.Format("Method '{0}' not supported", node.Method.Name));

            return node;

        }

        protected override Expression VisitUnary(UnaryExpression node)
        {

            if (node.NodeType == ExpressionType.Not)
            {
                whereBuilder.Append(" not ");
                Visit(node.Operand);
            }
            else if (node.NodeType == ExpressionType.Convert)
                Visit(node.Operand);
            else
                throw new NotSupportedException(string.Format("Unary operator '{0}' not supported", node.NodeType));

            return node;

        }

        protected override Expression VisitConstant(ConstantExpression node)
        {

            whereBuilder.Append(orm.Platform.addWhereParam(cmd, node.Value));
            return node;

        }
        
        private static bool isConstantNull(Expression exp)
        {
            return exp.NodeType == ExpressionType.Constant && ((ConstantExpression)exp).Value == null;
        }

        private void addLikeParam(MethodCallExpression node, string like)
        {
            MemberExpression member = (MemberExpression)node.Object;

            whereBuilder.Append(string.Format("{0}.{1} like {2}",
                tableObjectName,
                orm.Platform.getObjectName(member.Member),
                orm.Platform.addWhereParam(cmd, like)));

        }

        private static object getExpressionValue(Expression member)
        {
            UnaryExpression objectMember = Expression.Convert(member, typeof(object));
            Expression<Func<object>> getterLambda = Expression.Lambda<Func<object>>(objectMember);
            Func<object> getter = getterLambda.Compile();

            return getter();
        }

    }
}
