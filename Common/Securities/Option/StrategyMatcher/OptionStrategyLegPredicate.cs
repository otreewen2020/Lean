/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using QuantConnect.Util;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Defines a condition under which a particular <see cref="OptionPosition"/> can be combined with
    /// a preceding list of leg (also of type <see cref="OptionPosition"/>) to achieve a particular
    /// option strategy.
    /// </summary>
    public class OptionStrategyLegPredicate
    {
        /// <summary>
        /// The <see cref="BinaryComparison"/> used against the <see cref="Reference"/>
        /// </summary>
        public BinaryComparison Comparison { get; }

        /// <summary>
        /// The <see cref="IOptionStrategyLegPredicateReferenceValue"/> used to resolve comparands during matching
        /// </summary>
        public IOptionStrategyLegPredicateReferenceValue Reference { get; }

        /// <summary>
        /// The predicate function, capable of determining the the given arguments match this predicate
        /// </summary>
        public Func<List<OptionPosition>, OptionPosition, bool> Predicate { get; }

        /// <summary>
        /// The expression representing the predicate function. All other values are resolved from this expression
        /// and it is retained to provide a friendly to string implementation and convenient debugging
        /// </summary>
        public Expression<Func<List<OptionPosition>, OptionPosition, bool>> Expression { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyLegPredicate"/> class
        /// </summary>
        /// <param name="comparison">The <see cref="BinaryComparison"/> invoked</param>
        /// <param name="reference">The reference value, such as a strike price, encapsulated within the
        /// <see cref="IOptionStrategyLegPredicateReferenceValue"/> to enable resolving the value from different potential sets.</param>
        /// <param name="predicate">The compiled predicate expression</param>
        /// <param name="expression">The predicate expression, from which, all other values were derived.</param>
        public OptionStrategyLegPredicate(
            BinaryComparison comparison,
            IOptionStrategyLegPredicateReferenceValue reference,
            Func<List<OptionPosition>, OptionPosition, bool> predicate,
            Expression<Func<List<OptionPosition>, OptionPosition, bool>> expression
            )
        {
            Reference = reference;
            Predicate = predicate;
            Comparison = comparison;
            Expression = expression;
        }

        /// <summary>
        /// Determines whether or not the provided combination of preceding <paramref name="legs"/>
        /// and current <paramref name="position"/> adhere to this predicate's requirements.
        /// </summary>
        public bool Matches(List<OptionPosition> legs, OptionPosition position)
        {
            try
            {
                return Predicate(legs, position);
            }
            catch (InvalidOperationException)
            {
                // attempt to access option SecurityIdentifier values, such as strike, on the underlying
                // this simply means we don't match and can safely ignore this exception. now, this does
                // somewhat indicate a potential design flaw, but I content that this is better than having
                // to manage the underlying position separately throughout the entire matching process.
                return false;
            }
        }

        /// <summary>
        /// Filters the specified <paramref name="positions"/> by applying this predicate based on the referenced legs.
        /// </summary>
        public OptionPositionCollection Filter(List<OptionPosition> legs, OptionPositionCollection positions, bool includeUnderlying)
        {
            var referenceValue = Reference.Resolve(legs);
            switch (Reference.Target)
            {
                case PredicateTargetValue.Right:        return positions.Slice((OptionRight) referenceValue, includeUnderlying);
                case PredicateTargetValue.Strike:       return positions.Slice(Comparison, (decimal) referenceValue, includeUnderlying);
                case PredicateTargetValue.Expiration:   return positions.Slice(Comparison, (DateTime) referenceValue, includeUnderlying);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Creates a new <see cref="OptionStrategyLegPredicate"/> from the specified predicate <paramref name="expression"/>
        /// </summary>
        public static OptionStrategyLegPredicate Create(
            Expression<Func<List<OptionPosition>, OptionPosition, bool>> expression
            )
        {
            // expr must NOT include compound comparisons
            // expr is a lambda of one of the following forms:
            // (legs, position) => position.{target} {comparison} legs[i].{reference-target}
            // (legs, position) => legs[i].{reference-target} {comparison} position.{target}
            // (legs, position) => position.{target} {comparison} {literal-reference-target}
            // (legs, position) => {literal-reference-target} {comparison} position.{target}

            // we want to make the comparison of a common form, specifically:
            // position.{target} {comparison} {reference-target}
            // this is so when we invoke OptionPositionCollection we have the correct comparison type
            // for example, legs[0].Strike > position.Strike
            // needs to be inverted into position.Strike < legs[0].Strike
            // so we can call OptionPositionCollection.Slice(BinaryComparison.LessThan, legs[0].Strike);

            var legsParameter = expression.Parameters[0];
            var positionParameter = expression.Parameters[1];
            var binary = expression.OfType<BinaryExpression>().Single(e => e.NodeType.IsBinaryComparison());
            var comparison = BinaryComparison.FromExpressionType(binary.NodeType);
            var leftReference = CreateReferenceValue(legsParameter, positionParameter, binary.Left);
            var rightReference = CreateReferenceValue(legsParameter, positionParameter, binary.Right);
            if (leftReference != null && rightReference != null)
            {
                throw new ArgumentException($"The provided expression is not of the required form: {expression}");
            }

            // we want the left side to be null, indicating position.{target}
            // if not, then we need to flip the comparison operand
            var reference = rightReference;
            if (rightReference == null)
            {
                reference = leftReference;
                comparison = comparison.FlipOperands();
            }

            return new OptionStrategyLegPredicate(comparison, reference, expression.Compile(), expression);
        }

        /// <summary>
        /// Creates a new <see cref="IOptionStrategyLegPredicateReferenceValue"/> from the specified lambda parameters
        /// and expression to be evaluated.
        /// </summary>
        public static IOptionStrategyLegPredicateReferenceValue CreateReferenceValue(
            Expression legsParameter,
            Expression positionParameter,
            Expression expression
            )
        {
            // if we're referencing the position parameter then this isn't a reference value
            // this 'value' is the positions being matched in OptionPositionCollection
            // verify the legs parameter doesn't appear in here either
            var expressions = expression.AsEnumerable().ToList();
            var containsLegParameter = expressions.Any(e => ReferenceEquals(e, legsParameter));
            var containsPositionParameter = expressions.Any(e => ReferenceEquals(e, positionParameter));
            if (containsPositionParameter)
            {
                if (containsLegParameter)
                {
                    throw new NotSupportedException("Expressions containing references to both parameters (legs and positions) are not supported.");
                }

                // this expression is of the form position.Strike/position.Expiration/position.Right
                // and as such, is not a reference value, simply return null
                return null;
            }

            if (!containsLegParameter)
            {
                // this is a literal and we'll attempt to evaluate it.
                var value = System.Linq.Expressions.Expression.Lambda(expression).Compile().DynamicInvoke();
                if (value == null)
                {
                    throw new ArgumentNullException($"Failed to evaluate expression literal: {expressions}");
                }

                return ConstantOptionStrategyLegReferenceValue.Create(value);
            }

            // we're looking for an array indexer into the legs list
            var methodCall = expressions.Single<MethodCallExpression>();
            Debug.Assert(methodCall.Method.Name == "get_Item");
            // compile and dynamically invoke the argument to get_Item(x) {legs[x]}
            var arrayIndex = (int) System.Linq.Expressions.Expression.Lambda(methodCall.Arguments[0]).Compile().DynamicInvoke();

            // and then a member expression denoting the property (target)
            var member = expressions.Single<MemberExpression>().Member;
            var target = GetPredicateTargetValue(member.Name);

            return new OptionStrategyLegPredicateReferenceValue(arrayIndex, target);
        }

        private static PredicateTargetValue GetPredicateTargetValue(string memberName)
        {
            switch (memberName)
            {
                case nameof(OptionPosition.Right):      return PredicateTargetValue.Right;
                case nameof(OptionPosition.Strike):     return PredicateTargetValue.Strike;
                case nameof(OptionPosition.Expiration): return PredicateTargetValue.Expiration;
                default:
                    throw new NotImplementedException(
                        $"Failed to resolve member name '{memberName}' to {nameof(PredicateTargetValue)}"
                    );
            }
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return Expression.ToString();
        }
    }
}