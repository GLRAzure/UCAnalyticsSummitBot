using System;
using Microsoft.Bot.Builder.FormFlow;

[Serializable]
public class BasicForm
{
    [Prompt("Hi! What is the patient's diabetes pedigree function? (0.546)")]
    public string DiabetesPedigree { get; set; }

    [Prompt("What is the patient's BMI (weight in kg/(height in m)^2)? (36)")]
    public string BMI { get; set; }

    [Prompt("What is the patient's plasma glucose concentration? (109)")]
    public string PlasmaGlucose { get; set; }

    public static IForm<BasicForm> BuildForm()
    {
        // Builds an IForm<T> based on BasicForm
        return new FormBuilder<BasicForm>().Build();
    }

    public static IFormDialog<BasicForm> BuildFormDialog(FormOptions options = FormOptions.PromptInStart)
    {
        // Generate a new FormDialog<T> based on IForm<BasicForm>
        return FormDialog.FromForm(BuildForm, options);
    }
}