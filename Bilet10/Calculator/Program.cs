// Экзаменационный билет № 10, задание 3.
// Программа выполняет арифметические операции (сложение, вычитание, умножение, деление)
// с помощью отдельных методов класса Arithmetic. Методы вызываются из основной программы (Main).

using System;
using System.Globalization;
using System.Text;

namespace Calculator
{
    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Калькулятор — вызов методов";

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("========== КАЛЬКУЛЯТОР ==========");
                Console.WriteLine("1 — Сложение       (a + b)");
                Console.WriteLine("2 — Вычитание      (a - b)");
                Console.WriteLine("3 — Умножение      (a * b)");
                Console.WriteLine("4 — Деление        (a / b)");
                Console.WriteLine("5 — Все операции сразу");
                Console.WriteLine("0 — Выход");
                Console.Write("Выберите операцию: ");
                string choice = Console.ReadLine();

                if (choice == "0") break;
                if (choice != "1" && choice != "2" && choice != "3" && choice != "4" && choice != "5")
                {
                    Console.WriteLine("Нет такого пункта меню, введите число от 0 до 5.");
                    continue;
                }

                double a = ReadNumber("Введите первое число a: ");
                double b = ReadNumber("Введите второе число b: ");

                // Вызов методов из основной программы
                switch (choice)
                {
                    case "1":
                        PrintResult("+", a, b, Arithmetic.Add(a, b));
                        break;
                    case "2":
                        PrintResult("-", a, b, Arithmetic.Subtract(a, b));
                        break;
                    case "3":
                        PrintResult("*", a, b, Arithmetic.Multiply(a, b));
                        break;
                    case "4":
                        Divide(a, b);
                        break;
                    case "5":
                        PrintResult("+", a, b, Arithmetic.Add(a, b));
                        PrintResult("-", a, b, Arithmetic.Subtract(a, b));
                        PrintResult("*", a, b, Arithmetic.Multiply(a, b));
                        Divide(a, b);
                        break;
                }
            }

            Console.WriteLine("Работа программы завершена.");
        }

        /// <summary>Деление с обработкой ошибки деления на ноль.</summary>
        static void Divide(double a, double b)
        {
            try
            {
                PrintResult("/", a, b, Arithmetic.Divide(a, b));
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        /// <summary>Ввод числа с проверкой. Можно писать дробную часть через точку или запятую.</summary>
        static double ReadNumber(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string text = (Console.ReadLine() ?? "").Trim().Replace(',', '.');
                double value;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return value;
                Console.WriteLine("Это не число. Попробуйте ещё раз.");
            }
        }

        /// <summary>Вывод результата операции.</summary>
        static void PrintResult(string sign, double a, double b, double result)
        {
            Console.WriteLine("{0} {1} {2} = {3}", Format(a), sign, Format(b), Format(result));
        }

        static string Format(double x)
        {
            return Math.Round(x, 10).ToString(CultureInfo.CurrentCulture);
        }
    }
}
