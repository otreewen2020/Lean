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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Defines the item result type of <see cref="OptionStrategyLegDefinition.Match"/>, containing the number of
    /// times the leg definition matched the position (<see cref="Multiplier"/>) and applicable portion of the position.
    /// </summary>
    public struct OptionStrategyLegDefinitionMatch
    {
        /// <summary>
        /// The number of times the definition is able to match the position. For example,
        /// if the definition requires +2 contracts and the algorithm's position has +5
        /// contracts, then this multiplier would equal 2.
        /// </summary>
        public int Multiplier { get; }

        /// <summary>
        /// The position that was successfully matched with the total quantity matched. For example,
        /// if the definition requires +2 contracts and this multiplier equals 2, then this position
        /// would have a quantity of 4. This may be different than the remaining quantity available in
        /// the positions collection.
        /// </summary>
        public OptionPosition Position { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyLegDefinitionMatch"/> struct
        /// </summary>
        /// <param name="multiplier">The number of times the positions matched the leg definition</param>
        /// <param name="position">The position that matched the leg definition</param>
        public OptionStrategyLegDefinitionMatch(int multiplier, OptionPosition position)
        {
            Position = position;
            Multiplier = multiplier;
        }
    }

    /// <summary>
    /// Defines a single option leg in an option strategy. This definition supports direct
    /// match (does position X match the definition) and position collection filtering (filter
    /// collection to include matches)
    /// </summary>
    public class OptionStrategyLegDefinition : IEnumerable<OptionStrategyLegPredicate>
    {
        /// <summary>
        /// Gets the unit quantity
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// Gets the contract right
        /// </summary>
        public OptionRight Right { get; }

        private readonly OptionStrategyLegPredicate[] _predicates;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyLegDefinition"/> class
        /// </summary>
        /// <param name="right">The leg's contract right</param>
        /// <param name="quantity">The leg's unit quantity</param>
        /// <param name="predicates">The conditions a position must meet in order to match this definition</param>
        public OptionStrategyLegDefinition(OptionRight right, int quantity, IEnumerable<OptionStrategyLegPredicate> predicates)
        {
            Right = right;
            Quantity = quantity;
            _predicates = predicates.ToArray();
        }

        /// <summary>
        /// Yields all possible matches for this leg definition held within the collection of <paramref name="positions"/>
        /// </summary>
        /// <param name="legs">The preceding legs already matched for the parent strategy definition</param>
        /// <param name="positions">The remaining, unmatched positions available to be matched against</param>
        /// <returns>An enumerable of potential matches</returns>
        public IEnumerable<OptionStrategyLegDefinitionMatch> Match(
            IReadOnlyList<OptionPosition> legs,
            OptionPositionCollection positions
            )
        {
            foreach (var position in Filter(legs, positions))
            {
                var multiplier = position.Quantity / Quantity;
                if (multiplier != 0)
                {
                    yield return new OptionStrategyLegDefinitionMatch(multiplier,
                        position.WithQuantity(multiplier * Quantity)
                    );
                }
            }
        }

        /// <summary>
        /// Filters the provided <paramref name="positions"/> collection such that any remaining positions are all
        /// valid options that match this leg definition instance.
        /// </summary>
        public OptionPositionCollection Filter(IReadOnlyList<OptionPosition> legs, OptionPositionCollection positions)
        {
            foreach (var predicate in _predicates)
            {
                positions = predicate.Filter(legs, positions, true);
            }

            // at this point, every position in the positions
            // collection is a valid match for this definition
            return positions;
        }

        /// <summary>
        /// Creates the appropriate <see cref="OptionStrategy.LegData"/> for the specified <paramref name="match"/>
        /// </summary>
        public OptionStrategy.LegData CreateLegData(OptionStrategyLegDefinitionMatch match)
        {
            return CreateLegData(
                match.Position.Symbol,
                match.Position.Quantity / Quantity
            );
        }

        /// <summary>
        /// Creates the appropriate <see cref="OptionStrategy.LegData"/> with the specified <paramref name="quantity"/>
        /// </summary>
        public static OptionStrategy.LegData CreateLegData(Symbol symbol, int quantity)
        {
            if (symbol.SecurityType == SecurityType.Option)
            {
                return OptionStrategy.OptionLegData.Create(quantity, symbol);
            }

            return OptionStrategy.UnderlyingLegData.Create(quantity);
        }

        /// <summary>
        /// Determines whether or not this leg definition matches the specified <paramref name="position"/>,
        /// and if so, what the resulting quantity of the <see cref="OptionStrategy.OptionLegData"/> should be.
        /// </summary>
        public bool TryMatch(OptionPosition position, out OptionStrategy.LegData leg)
        {
            if (Right != position.Right ||
                Math.Sign(Quantity) != Math.Sign(position.Quantity))
            {
                leg = null;
                return false;
            }

            var quantity = position.Quantity / Quantity;
            if (quantity == 0)
            {
                leg = null;
                return false;
            }

            leg = position.Symbol.SecurityType == SecurityType.Option
                ? (OptionStrategy.LegData) OptionStrategy.OptionLegData.Create(quantity, position.Symbol)
                : OptionStrategy.UnderlyingLegData.Create(quantity);

            return true;
        }

        /// <summary>
        /// Creates a new <see cref="OptionStrategyLegDefinition"/> matching the specified parameters
        /// </summary>
        public static OptionStrategyLegDefinition Create(OptionRight right, int quantity,
            IEnumerable<Expression<Func<IReadOnlyList<OptionPosition>, OptionPosition, bool>>> predicates
            )
        {
            return new OptionStrategyLegDefinition(right, quantity,
                // sort predicates such that indexed predicates are evaluated first
                // this leaves fewer positions to be evaluated by the full table scan
                predicates.Select(OptionStrategyLegPredicate.Create).OrderBy(p => p.IsIndexed ? 0 : 1)
            );
        }

        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<OptionStrategyLegPredicate> GetEnumerator()
        {
            foreach (var predicate in _predicates)
            {
                yield return predicate;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
