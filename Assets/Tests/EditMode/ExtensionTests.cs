using System;
using System.Collections.Generic;
using System.Linq;
using ImmoralityGaming.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class IntegerExtensionTests
    {
        [Test]
        public void IsOdd_OddNumber_ReturnsTrue()
        {
            Assert.IsTrue(1.IsOdd());
            Assert.IsTrue(3.IsOdd());
            Assert.IsTrue(99.IsOdd());
        }

        [Test]
        public void IsOdd_EvenNumber_ReturnsFalse()
        {
            Assert.IsFalse(0.IsOdd());
            Assert.IsFalse(2.IsOdd());
            Assert.IsFalse(100.IsOdd());
        }

        [Test]
        public void IsOdd_NegativeOdd_ReturnsTrue()
        {
            Assert.IsTrue((-1).IsOdd());
            Assert.IsTrue((-3).IsOdd());
        }

        [Test]
        public void IsEven_EvenNumber_ReturnsTrue()
        {
            Assert.IsTrue(0.IsEven());
            Assert.IsTrue(2.IsEven());
            Assert.IsTrue(100.IsEven());
        }

        [Test]
        public void IsEven_OddNumber_ReturnsFalse()
        {
            Assert.IsFalse(1.IsEven());
            Assert.IsFalse(3.IsEven());
        }

        [Test]
        public void IsBetween_InsideRange_ReturnsTrue()
        {
            Assert.IsTrue(5.IsBetween(1, 10));
            Assert.IsTrue(5.IsBetween(5, 10));
            Assert.IsTrue(10.IsBetween(5, 10));
        }

        [Test]
        public void IsBetween_OutsideRange_ReturnsFalse()
        {
            Assert.IsFalse(0.IsBetween(1, 10));
            Assert.IsFalse(11.IsBetween(1, 10));
        }

        [Test]
        public void IsBetween_SameMinMax_OnlyExactMatch()
        {
            Assert.IsTrue(5.IsBetween(5, 5));
            Assert.IsFalse(4.IsBetween(5, 5));
        }

        [Test]
        public void ToTriangleNumber_KnownValues()
        {
            // T(1) = 1
            Assert.AreEqual(1, 1.ToTriangleNumber());
            // T(4) = 4+3+2+1 = 10
            Assert.AreEqual(10, 4.ToTriangleNumber());
            // T(5) = 15
            Assert.AreEqual(15, 5.ToTriangleNumber());
            // T(0) = 0
            Assert.AreEqual(0, 0.ToTriangleNumber());
        }
    }

    public class EnumerableExtensionTests
    {
        [Test]
        public void DistinctBy_RemovesDuplicateKeys()
        {
            var items = new[] { "apple", "avocado", "banana", "blueberry", "cherry" };

            var result = items.DistinctBy(s => s[0]).ToList();

            // One per first letter: a, b, c
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("apple", result[0]);
            Assert.AreEqual("banana", result[1]);
            Assert.AreEqual("cherry", result[2]);
        }

        [Test]
        public void DistinctBy_EmptySequence_ReturnsEmpty()
        {
            var items = new string[0];

            var result = items.DistinctBy(s => s).ToList();

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void DistinctBy_AllUnique_ReturnsAll()
        {
            var items = new[] { 1, 2, 3 };

            var result = items.DistinctBy(x => x).ToList();

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void MinBy_ReturnsSmallestProjection()
        {
            var items = new[] { "banana", "fig", "apple", "cherry" };

            var result = items.MinBy(s => s.Length);

            Assert.AreEqual("fig", result);
        }

        [Test]
        public void MinBy_SingleElement_ReturnsThat()
        {
            var items = new[] { 42 };

            var result = items.MinBy(x => x);

            Assert.AreEqual(42, result);
        }

        [Test]
        public void MinBy_EmptySequence_Throws()
        {
            var items = new int[0];

            Assert.Throws<InvalidOperationException>(() => items.MinBy(x => x));
        }

        [Test]
        public void MaxBy_ReturnsLargestProjection()
        {
            var items = new[] { "fig", "banana", "cherry", "apple" };

            var result = items.MaxBy(s => s.Length);

            Assert.AreEqual("banana", result);
        }

        [Test]
        public void MaxBy_SingleElement_ReturnsThat()
        {
            var items = new[] { 7 };

            var result = items.MaxBy(x => x);

            Assert.AreEqual(7, result);
        }

        [Test]
        public void MaxBy_EmptySequence_Throws()
        {
            var items = new int[0];

            Assert.Throws<InvalidOperationException>(() => items.MaxBy(x => x));
        }

        [Test]
        public void ToCommaSeperatedString_Default()
        {
            var items = new[] { "a", "b", "c" };

            var result = items.ToCommaSeperatedString();

            Assert.AreEqual("a, b, c", result);
        }

        [Test]
        public void ToCommaSeperatedString_CustomSeparator()
        {
            var items = new[] { "a", "b", "c" };

            var result = items.ToCommaSeperatedString(" | ");

            Assert.AreEqual("a | b | c", result);
        }

        [Test]
        public void ToCommaSeperatedString_SingleItem()
        {
            var items = new[] { "only" };

            var result = items.ToCommaSeperatedString();

            Assert.AreEqual("only", result);
        }

        [Test]
        public void ToCommaSeperatedString_Empty_ReturnsEmpty()
        {
            var items = new string[0];

            var result = items.ToCommaSeperatedString();

            Assert.AreEqual("", result);
        }
    }

    public class ListExtensionTests
    {
        [Test]
        public void TakeRandom_EmptyList_ReturnsDefault()
        {
            var list = new List<int>();

            var result = list.TakeRandom();

            Assert.AreEqual(0, result);
        }

        [Test]
        public void TakeRandom_SingleItem_ReturnsThat()
        {
            var list = new List<string> { "only" };

            var result = list.TakeRandom();

            Assert.AreEqual("only", result);
        }

        [Test]
        public void TakeRandom_ReturnsItemFromList()
        {
            var list = new List<int> { 10, 20, 30 };

            var result = list.TakeRandom();

            Assert.IsTrue(list.Contains(result));
        }

        [Test]
        public void TakeRandom_WithPredicate_NoMatch_ReturnsDefault()
        {
            var list = new List<int> { 1, 3, 5 };

            var result = list.TakeRandom(x => x > 100);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void TakeRandom_WithPredicate_ReturnsMatchingItem()
        {
            var list = new List<int> { 1, 2, 3, 4, 5 };

            var result = list.TakeRandom(x => x > 3);

            Assert.IsTrue(result > 3);
        }

        [Test]
        public void Shuffle_PreservesAllElements()
        {
            var list = new List<int> { 1, 2, 3, 4, 5 };

            list.Shuffle();

            Assert.AreEqual(5, list.Count);
            Assert.IsTrue(list.Contains(1));
            Assert.IsTrue(list.Contains(2));
            Assert.IsTrue(list.Contains(3));
            Assert.IsTrue(list.Contains(4));
            Assert.IsTrue(list.Contains(5));
        }

        [Test]
        public void Shuffle_EmptyList_DoesNotThrow()
        {
            var list = new List<int>();

            Assert.DoesNotThrow(() => list.Shuffle());
        }

        [Test]
        public void Shuffle_SingleItem_ReturnsSame()
        {
            var list = new List<int> { 42 };

            list.Shuffle();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(42, list[0]);
        }

        [Test]
        public void AddDistinct_NewItem_Adds()
        {
            var list = new List<string> { "a", "b" };

            list.AddDistinct("c");

            Assert.AreEqual(3, list.Count);
            Assert.Contains("c", list);
        }

        [Test]
        public void AddDistinct_ExistingItem_DoesNotAdd()
        {
            var list = new List<string> { "a", "b" };

            list.AddDistinct("a");

            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void AddDistinct_EmptyList_Adds()
        {
            var list = new List<string>();

            list.AddDistinct("first");

            Assert.AreEqual(1, list.Count);
        }
    }

    public class Vector3ExtensionTests
    {
        [Test]
        public void ToFloat_SumsComponents()
        {
            var v = new Vector3(1f, 2f, 3f);

            Assert.AreEqual(6f, v.ToFloat(), 0.001f);
        }

        [Test]
        public void ToFloat_Zero_ReturnsZero()
        {
            Assert.AreEqual(0f, Vector3.zero.ToFloat(), 0.001f);
        }

        [Test]
        public void ToFloat_NegativeComponents()
        {
            var v = new Vector3(-1f, 2f, -3f);

            Assert.AreEqual(-2f, v.ToFloat(), 0.001f);
        }

        [Test]
        public void ToInt_RoundsSum()
        {
            var v = new Vector3(1.4f, 2.3f, 3.1f);

            // Sum = 6.8, rounded = 7
            Assert.AreEqual(7, v.ToInt());
        }

        [Test]
        public void ToInt_ExactInteger()
        {
            var v = new Vector3(1f, 2f, 3f);

            Assert.AreEqual(6, v.ToInt());
        }

        [Test]
        public void RoundToNearest_RoundsEachComponent()
        {
            var v = new Vector3(1.4f, 2.6f, 3.5f);

            var result = v.RoundToNearest();

            Assert.AreEqual(1f, result.x, 0.001f);
            Assert.AreEqual(3f, result.y, 0.001f);
            Assert.AreEqual(4f, result.z, 0.001f);
        }

        [Test]
        public void RoundToNearest_AlreadyInteger_Unchanged()
        {
            var v = new Vector3(1f, 2f, 3f);

            var result = v.RoundToNearest();

            Assert.AreEqual(v, result);
        }

        [Test]
        public void RoundToNearest_Negative()
        {
            var v = new Vector3(-1.3f, -2.7f, 0.7f);

            var result = v.RoundToNearest();

            Assert.AreEqual(-1f, result.x, 0.001f);
            Assert.AreEqual(-3f, result.y, 0.001f);
            Assert.AreEqual(1f, result.z, 0.001f);
        }
    }
}
