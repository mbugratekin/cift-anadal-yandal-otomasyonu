using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CapYandal.Web.Models;


public sealed class SayisalNotModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var input=context.ValueProvider.GetValue(context.ModelName);
        if(input==ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName,input);
        var text=input.FirstValue?.Trim();
        if(string.IsNullOrEmpty(text)) { context.Result=ModelBindingResult.Success(null);return Task.CompletedTask; }
        if(!Regex.IsMatch(text,@"^[0-9]{1,3}([.,][0-9]{1,2})?$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)) ||
            !decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value) || value is <0 or >100)
            context.ModelState.TryAddModelError(context.ModelName,"Sayısal notu 0–100 arasında, en fazla iki ondalıkla girin.");
        else context.Result=ModelBindingResult.Success(value);
        return Task.CompletedTask;
    }
}
