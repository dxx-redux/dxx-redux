/*
    Copyright (c) 2020 The LibDescent Team.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents a dictionary where the keys are ordered according to their order of insertion.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    public class InsertionOrderedDictionary<K, V> : IDictionary<K, V>
    {
        private Dictionary<K, V> data;
        private List<K> keyOrder;

        public InsertionOrderedDictionary()
        {
            data = new Dictionary<K, V>();
            keyOrder = new List<K>();
        }

        public InsertionOrderedDictionary(IEnumerable<KeyValuePair<K, V>> pairs) : this()
        {
            foreach (KeyValuePair<K, V> pair in pairs)
                Add(pair);
        }

        public V this[K key] { get => data[key]; set => data[key] = value; }

        public ICollection<K> Keys => keyOrder;

        public ICollection<V> Values => keyOrder.Select(k => data[k]).ToList();

        public int Count => data.Count;

        public bool IsReadOnly => false;

        public void Add(K key, V value)
        {
            if (!keyOrder.Contains(key))
                keyOrder.Add(key);
            data.Add(key, value);
        }

        public void Add(KeyValuePair<K, V> item)
        {
            data.Add(item.Key, item.Value);
        }

        /// <summary>
        /// Moves the given key to be the first in order.
        /// </summary>
        /// <param name="key">The key to move.</param>
        /// <returns>Whether the key was found and moved to the beginning of the key order.</returns>
        public bool MoveFront(K key)
        {
            if (keyOrder.Contains(key))
            {
                keyOrder.Remove(key);
                keyOrder.Insert(0, key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Moves the given key to be the last in order.
        /// </summary>
        /// <param name="key">The key to move.</param>
        /// <returns>Whether the key was found and moved to the end of the key order.</returns>
        public bool MoveBack(K key)
        {
            if (keyOrder.Contains(key))
            {
                keyOrder.Remove(key);
                keyOrder.Add(key);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            data.Clear();
            keyOrder.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            return data.ContainsKey(item.Key) && EqualityComparer<V>.Default.Equals(data[item.Key], item.Value);
        }

        public bool ContainsKey(K key)
        {
            return data.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            foreach (K key in keyOrder)
                array[arrayIndex++] = new KeyValuePair<K, V>(key, data[key]);
        }

        public bool Remove(K key)
        {
            return data.Remove(key) && keyOrder.Remove(key);
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            if (!Contains(item))
                return false;
            return Remove(item.Key);
        }

        public bool TryGetValue(K key, out V value)
        {
            return data.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return new InsertionOrderedDictionaryIEnumerable(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new InsertionOrderedDictionaryIEnumerable(this);
        }

        public class InsertionOrderedDictionaryIEnumerable : IEnumerator<KeyValuePair<K, V>>
        {
            InsertionOrderedDictionary<K, V> parent;
            KeyValuePair<K, V> current = default;
            IEnumerator<K> keyEnumerator;

            public InsertionOrderedDictionaryIEnumerable(InsertionOrderedDictionary<K, V> parent)
            {
                this.parent = parent;
                Reset();
            }

            public KeyValuePair<K, V> Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
                parent = null;
                current = default;
            }

            public bool MoveNext()
            {
                bool result = keyEnumerator.MoveNext();
                if (result)
                    current = new KeyValuePair<K, V>(keyEnumerator.Current, parent[keyEnumerator.Current]);
                return result;
            }

            public void Reset()
            {
                current = default;
                keyEnumerator = parent.keyOrder.GetEnumerator();
            }
        }
    }
}