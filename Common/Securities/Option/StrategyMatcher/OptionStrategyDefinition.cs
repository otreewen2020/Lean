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
using System.Linq;
using System.Linq.Expressions;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    public class OptionStrategyDefinition
    {
        public string Name { get; }

        private readonly List<OptionStrategyLegDefinition> _legs;

        public OptionStrategyDefinition(string name, IEnumerable<OptionStrategyLegDefinition> legs)
        {
            Name = name;
            _legs = legs.ToList();
        }

        public static OptionStrategyDefinition Create(string name, params OptionStrategyLegDefinition[] legs)
        {
            return new OptionStrategyDefinition(name, legs);
        }

        public static OptionStrategyLegDefinition Call(int quantity,
            params Expression<Func<List<OptionPosition>, OptionPosition, bool>>[] predicates
            )
        {
            return OptionStrategyLegDefinition.Create(OptionRight.Call, quantity, predicates);
        }

        public static OptionStrategyLegDefinition Put(int quantity,
            params Expression<Func<List<OptionPosition>, OptionPosition, bool>>[] predicates
            )
        {
            return OptionStrategyLegDefinition.Create(OptionRight.Put, quantity, predicates);
        }
    }
}
