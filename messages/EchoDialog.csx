using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// For more information about this template visit http://aka.ms/azurebots-csharp-basic
[Serializable]
public class EchoDialog : IDialog<object>
{
    protected int count = 1;
    protected string patientNo = "100";
    protected string diabetesPedigree = "0.546";
    protected string bmi = "36";
    protected string plasmaGlucose = "109";

    public Task StartAsync(IDialogContext context)
    {
        try
        {
            context.Wait(MessageReceivedAsync);
        }
        catch (OperationCanceledException error)
        {
            return Task.FromCanceled(error.CancellationToken);
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }

        return Task.CompletedTask;
    }

    public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
    {
        var message = await argument;
        if (message.Text == "reset")
        {
            PromptDialog.Confirm(
                context,
                AfterResetAsync,
                "Are you sure you want to reset the count?",
                "Didn't get that!",
                promptStyle: PromptStyle.Auto);
        }
        else if (message.Text.ToLower() == "predict diabetes")
        {
            PromptDialog.Text(
                context,
                AfterPatientEntered,
                "Please enter the Patient Number:",
                "Invalid Number.",
                3);
        }
        else
        {
            await context.PostAsync($"{this.count++}: You said {message.Text.ToLower()}");
            context.Wait(MessageReceivedAsync);
        }
    }
    
    public async Task AfterPatientEntered(IDialogContext context, IAwaitable<string> argument)
    {
        var num = await argument;
        this.patientNo = num;
        
        PromptDialog.Text(
                context,
                AfterDiabetesPedigreeEntered,
                "Please enter the Diabetes Pedigree Function (0.0-3.0):",
                "Invalid Number.",
                3);
    }
    
    public async Task AfterDiabetesPedigreeEntered(IDialogContext context, IAwaitable<string> argument)
    {
        var num = await argument;
        this.diabetesPedigree = num;
        
        PromptDialog.Text(
                context,
                AfterBMIEntered,
                "Please enter Body Mass Index (0.0-70.0):",
                "Invalid Number.",
                3);
    }
    
    public async Task AfterBMIEntered(IDialogContext context, IAwaitable<string> argument)
    {
        var num = await argument;
        this.bmi = num;
        
        PromptDialog.Text(
                context,
                AfterPlasmaEntered,
                "Please enter Plasma Glucose Concentration (0-200):",
                "Invalid Number.",
                3);
    }
    
    public async Task AfterPlasmaEntered(IDialogContext context, IAwaitable<string> argument)
    {
        var num = await argument;
        this.plasmaGlucose = num;
        
        var prediction = PredictDiabetes().Result;
        
        await context.PostAsync(prediction);
        
        context.Wait(MessageReceivedAsync);
    }
    
    public async Task<string> PredictDiabetes()
    {
        var responseString = String.Empty;
        
        using (var client = new HttpClient())
        {
            var scoreRequest = new
            {

                Inputs = new Dictionary<string, StringTable> () { 
                    { 
                        "input1", 
                        new StringTable() 
                        {
                            ColumnNames = new string[] {"Diabetes pedigree function", "Body mass index (weight in kg/(height in m)^2)", "Plasma glucose concentration a 2 hours in an oral glucose tolerance test", "2-Hour serum insulin (mu U/ml)", "Age (years)", "Diastolic blood pressure (mm Hg)", "Number of times pregnant", "Triceps skin fold thickness (mm)", "Patient Number", "Class variable (0 or 1)"},
                            Values = new string[,] { { this.diabetesPedigree, this.bmi, this.plasmaGlucose, "0", "60", "75", "0", "1", this.patientNo, "0" } }
                        }
                    },
                },
                GlobalParameters = new Dictionary<string, string>() {}
            };
            const string apiKey = "2ErD/qDc2XXdN9iujeI1AGTTjQqwjaU+f+01/MvP2SX0fXI6Sv/dGFcRAOua6eZcvQGxIUi5Gs1IVmJst/fbMg=="; // Replace this with the API key for the web service
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", apiKey);

            client.BaseAddress = new Uri("https://ussouthcentral.services.azureml.net/workspaces/2ef63dec5f4148fd972df971e78ebd8a/services/669384b9e8af4ea88d288b15dc9130a9/execute?api-version=2.0&details=true");
            
            HttpResponseMessage response = await client.PostAsJsonAsync("", scoreRequest);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                dynamic mlResponse = JObject.Parse(result);
                dynamic mlValues = mlResponse.Results.output1.value.Values;
                dynamic mlValue = mlValues[0][1];
                
                responseString = mlValue + " Probability";
            }
            else
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                responseString = "Oh shucks, looks like we have an issue: " + response.StatusCode + ", " + responseContent;
            }
        }

        
        return responseString;
    }

    public async Task AfterResetAsync(IDialogContext context, IAwaitable<bool> argument)
    {
        var confirm = await argument;
        if (confirm)
        {
            this.count = 1;
            await context.PostAsync("Reset count.");
        }
        else
        {
            await context.PostAsync("Did not reset count.");
        }
        context.Wait(MessageReceivedAsync);
    }
}