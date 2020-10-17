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
        /// Creates a new <see cref="OptionStrategyLegDefinition"/> matching the specified parameters
        /// </summary>
        public static OptionStrategyLegDefinition Create(OptionRight right, int quantity,
            IEnumerable<Expression<Func<List<OptionPosition>, OptionPosition, bool>>> predicates
            )
        {
            return new OptionStrategyLegDefinition(right, quantity,
                predicates.Select(OptionStrategyLegPredicate.Create)
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
