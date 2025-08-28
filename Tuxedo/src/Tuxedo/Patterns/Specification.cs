using System;
using System.Linq.Expressions;

namespace Tuxedo.Patterns
{
    /// <summary>
    /// Base class for implementing the Specification pattern
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public abstract class Specification<T>
    {
        /// <summary>
        /// Gets the expression that defines this specification
        /// </summary>
        public abstract Expression<Func<T, bool>> ToExpression();

        /// <summary>
        /// Checks if an entity satisfies this specification
        /// </summary>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if the entity satisfies the specification</returns>
        public bool IsSatisfiedBy(T entity)
        {
            var predicate = ToExpression().Compile();
            return predicate(entity);
        }

        /// <summary>
        /// Combines this specification with another using AND logic
        /// </summary>
        /// <param name="specification">The specification to combine with</param>
        /// <returns>A new specification representing the AND combination</returns>
        public Specification<T> And(Specification<T> specification)
        {
            return new AndSpecification<T>(this, specification);
        }

        /// <summary>
        /// Combines this specification with another using OR logic
        /// </summary>
        /// <param name="specification">The specification to combine with</param>
        /// <returns>A new specification representing the OR combination</returns>
        public Specification<T> Or(Specification<T> specification)
        {
            return new OrSpecification<T>(this, specification);
        }

        /// <summary>
        /// Negates this specification
        /// </summary>
        /// <returns>A new specification representing the negation</returns>
        public Specification<T> Not()
        {
            return new NotSpecification<T>(this);
        }
    }

    internal class AndSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public AndSpecification(Specification<T> left, Specification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var leftExpression = _left.ToExpression();
            var rightExpression = _right.ToExpression();
            
            var parameter = Expression.Parameter(typeof(T));
            var leftBody = ReplaceParameter(leftExpression.Body, leftExpression.Parameters[0], parameter);
            var rightBody = ReplaceParameter(rightExpression.Body, rightExpression.Parameters[0], parameter);
            
            var body = Expression.AndAlso(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }
    }

    internal class OrSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public OrSpecification(Specification<T> left, Specification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var leftExpression = _left.ToExpression();
            var rightExpression = _right.ToExpression();
            
            var parameter = Expression.Parameter(typeof(T));
            var leftBody = ReplaceParameter(leftExpression.Body, leftExpression.Parameters[0], parameter);
            var rightBody = ReplaceParameter(rightExpression.Body, rightExpression.Parameters[0], parameter);
            
            var body = Expression.OrElse(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }
    }

    internal class NotSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _specification;

        public NotSpecification(Specification<T> specification)
        {
            _specification = specification;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var expression = _specification.ToExpression();
            var body = Expression.Not(expression.Body);
            return Expression.Lambda<Func<T, bool>>(body, expression.Parameters);
        }
    }

    internal class ParameterReplacer : ExpressionVisitor
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