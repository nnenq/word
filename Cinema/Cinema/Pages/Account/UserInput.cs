using System.ComponentModel.DataAnnotations;

namespace Cinema.Pages.Account;

/// <summary>Правила проверки полей пользователя (общие для регистрации, профиля и админ-панели).</summary>
public static class UserRules
{
    public const string LoginPattern = "^[A-Za-z0-9_]{3,20}$";
    public const string LoginMessage = "Логин: от 3 до 20 латинских букв, цифр или знака подчёркивания.";
    public const string NamePattern = @"^[A-Za-zА-Яа-яЁё][A-Za-zА-Яа-яЁё\s\-\.]{1,99}$";
    public const string NameMessage = "ФИО: от 2 до 100 символов, только буквы, пробел, дефис и точка.";
    public const string PasswordPattern = @"^(?=.*[A-Za-zА-Яа-яЁё])(?=.*\d).{6,50}$";
    public const string PasswordMessage = "Пароль: не короче 6 символов, должен содержать хотя бы одну букву и одну цифру.";
}

public class RegisterInput
{
    [Required(ErrorMessage = "Введите ФИО.")]
    [RegularExpression(UserRules.NamePattern, ErrorMessage = UserRules.NameMessage)]
    [Display(Name = "ФИО")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Введите логин.")]
    [RegularExpression(UserRules.LoginPattern, ErrorMessage = UserRules.LoginMessage)]
    [Display(Name = "Логин")]
    public string Login { get; set; } = "";

    [Required(ErrorMessage = "Введите электронную почту.")]
    [EmailAddress(ErrorMessage = "Электронная почта указана неверно. Пример: name@mail.ru")]
    [StringLength(100)]
    [Display(Name = "Электронная почта")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль.")]
    [RegularExpression(UserRules.PasswordPattern, ErrorMessage = UserRules.PasswordMessage)]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Повторите пароль.")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
    [DataType(DataType.Password)]
    [Display(Name = "Повтор пароля")]
    public string Confirm { get; set; } = "";
}
