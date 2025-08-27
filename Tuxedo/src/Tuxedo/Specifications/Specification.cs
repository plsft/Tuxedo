using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Tuxedo.Specifications
{
    public abstract class Specification<T> : ISpecification<T>
    {
        protected Specification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        protected Specification()
        {
            Criteria = x => true;
        }

        public Expression<Func<T, bool>> Criteria { get; private set; }
        public List<Expression<Func<T, object>>> Includes { get; } = new List<Expression<Func<T, object>>>();
        public List<string> IncludeStrings { get; } = new List<string>();
        public Expression<Func<T, object>>? OrderBy { get; private set; }
        public Expression<Func<T, object>>? OrderByDescending { get; private set; }
        public Expression<Func<T, object>>? GroupBy { get; private set; }
        public int Take { get; private set; }
        public int Skip { get; private set; }
        public bool IsPagingEnabled { get; private set; }
        public bool AsNoTracking { get; private set; } = true;

        protected virtual void AddInclude(Expression<Func<T, object>> includeExpression)
        {
            Includes.Add(includeExpression);
        }

        protected virtual void AddInclude(string includeString)
        {
            IncludeStrings.Add(includeString);
        }

        protected virtual void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }

        protected virtual void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
        {
            OrderBy = orderByExpression;
        }

        protected virtual void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
        {
            OrderByDescending = orderByDescExpression;
        }

        protected virtual void ApplyGroupBy(Expression<Func<T, object>> groupByExpression)
        {
            GroupBy = groupByExpression;
        }

        protected virtual void ApplyNoTracking()
        {
            AsNoTracking = true;
        }

        protected virtual void AddCriteria(Expression<Func<T, bool>> criteria)
        {
            Criteria = CombineWithAnd(Criteria, criteria);
        }

        protected virtual void AddOrCriteria(Expression<Func<T, bool>> criteria)
        {
            Criteria = CombineWithOr(Criteria, criteria);
        }

        public virtual bool IsSatisfiedBy(T entity)
        {
            var compiledCriteria = Criteria.Compile();
            return compiledCriteria(entity);
        }

        private Expression<Func<T, bool>> CombineWithAnd(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var body = Expression.AndAlso(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private Expression<Func<T, bool>> CombineWithOr(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var body = Expression.OrElse(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }

        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : node;
            }
        }
    }
}