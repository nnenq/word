using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StudyCabinet.Pages;

[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public int Code { get; set; }
    public string Title => Code switch { 404 => "Страница не найдена", 403 => "Доступ запрещён", 400 => "Неверный запрос", _ => "Произошла ошибка" };
    public string Text => Code switch
    {
        404 => "Такой страницы нет. Возможно, лекция или дисциплина была удалена.",
        403 => "У вас нет прав на это действие.",
        400 => "Запрос не прошёл проверку. Обновите страницу и попробуйте ещё раз.",
        _ => "Ошибка записана в журнал. Попробуйте ещё раз или обратитесь к преподавателю.",
    };
    public void OnGet(int? code) => Code = code ?? 500;
    public void OnPost(int? code) => Code = code ?? 500;
}
