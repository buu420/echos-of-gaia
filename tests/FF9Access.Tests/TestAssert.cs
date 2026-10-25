using System;
using System.Collections.Generic;
namespace FF9Access.Tests
{
    internal static class TestAssert
    {
        public static void True(bool value) { if (!value) throw new Exception("Expected true"); }
        public static void False(bool value) { if (value) throw new Exception("Expected false"); }
        public static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
        public static void Near(float expected, float actual, float tolerance) { if (float.IsNaN(actual) || Math.Abs(expected - actual) > tolerance) throw new Exception("Expected " + expected + " +/- " + tolerance + ", got " + actual); }
    }
}
