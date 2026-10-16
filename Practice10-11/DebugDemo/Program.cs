// Практическое занятие № 10-11. Отладка программы и обработка исключений.
// Программа считает факториал числа n и сумму ряда 1/0! + 1/1! + ... + 1/n! (она стремится к числу e = 2,71828...).
// Ошибки ввода и переполнение обрабатываются через исключения.

using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("Расчёт факториала и суммы ряда 1/k!");

while (true)
{
    Console.Write("\nВведите n от 0 до 20 (пустая строка — выход): ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    try
    {
        int n = Calculator.ReadNumber(input);
        long factorial = Calculator.Factorial(n);
        double sum = Calculator.SeriesSum(n);

        Console.WriteLine($"{n}! = {factorial}");
        Console.WriteLine($"Сумма ряда = {sum:F10}");
        Console.WriteLine($"Отличие от числа e = {Math.Abs(Math.E - sum):E2}");
    }
    catch (FormatException)
    {
        Console.WriteLine("Ошибка: нужно ввести целое число.");
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine("Ошибка: " + ex.Message);
    }
    catch (OverflowException)
    {
        Console.WriteLine("Ошибка: результат слишком большой для типа long.");
    }
    finally
    {
        Console.WriteLine("Расчёт завершён.");
    }
}

static class Calculator
{
    public const int MaxN = 20;

    /// <summary>Преобразует строку в число и проверяет диапазон.</summary>
    public static int ReadNumber(string text)
    {
        int n = int.Parse(text.Trim());   // при вводе букв — FormatException
        if (n < 0 || n > MaxN)
            throw new ArgumentOutOfRangeException(null, $"n должно быть от 0 до {MaxN}, введено {n}.");
        return n;
    }

    /// <summary>Факториал n! = 1 · 2 · ... · n.</summary>
    public static long Factorial(int n)
    {
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result = checked(result * i);  // при переполнении — OverflowException
        }
        return result;
    }

    /// <summary>Сумма ряда 1/0! + 1/1! + ... + 1/n!.</summary>
    public static double SeriesSum(int n)
    {
        double sum = 0;
        double term = 1;                   // текущее слагаемое 1/k!
        for (int k = 0; k <= n; k++)
        {
            if (k > 0) term /= k;
            sum += term;
        }
        return sum;
    }
}
