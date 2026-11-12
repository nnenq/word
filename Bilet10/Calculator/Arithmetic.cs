// Отдельные методы для арифметических операций.
// Каждый метод принимает два числа и возвращает результат.

using System;

namespace Calculator
{
    public static class Arithmetic
    {
        /// <summary>Сложение: a + b</summary>
        public static double Add(double a, double b)
        {
            return a + b;
        }

        /// <summary>Вычитание: a - b</summary>
        public static double Subtract(double a, double b)
        {
            return a - b;
        }

        /// <summary>Умножение: a * b</summary>
        public static double Multiply(double a, double b)
        {
            return a * b;
        }

        /// <summary>Деление: a / b. На ноль делить нельзя — выбрасывается исключение.</summary>
        public static double Divide(double a, double b)
        {
            if (b == 0)
                throw new DivideByZeroException("На ноль делить нельзя.");
            return a / b;
        }
    }
}
