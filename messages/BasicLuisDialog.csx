using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

// For more information about this template visit http://aka.ms/azurebots-csharp-luis
[Serializable]
public class BasicLuisDialog : LuisDialog<object>
{
    protected string patientNo = "100";
    protected string diabetesPedigree = "0.546";
    protected string bmi = "36";
    protected string plasmaGlucose = "109";
    
    public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(Utils.GetAppSetting("LuisAppId"), Utils.GetAppSetting("LuisAPIKey"))))
    {
    }

    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        await context.SayAsync($"I'll check with your assistant."); //
        await context.SayAsync(QnAMaker(result.Query),QnAMaker(result.Query));
        context.Wait(MessageReceived);
    }
    
    private string QnAMaker(string userQuery)
    {
        string responseString = string.Empty;

        var query = userQuery; //User Query
        var knowledgebaseId = Utils.GetAppSetting("QnAKnowledgebaseId"); // Use knowledge base id created.
        var qnamakerSubscriptionKey = Utils.GetAppSetting("QnASubscriptionKey"); //Use subscription key assigned to you.
        
        //Build the URI
        Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
        var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");
        
        //Add the question as part of the body
        var postBody = $"{{\"question\": \"{query}\"}}";
        
        //Send the POST request
        using (WebClient client = new WebClient())
        {
            //Set the encoding to UTF8
            client.Encoding = System.Text.Encoding.UTF8;
        
            //Add the subscription key header
            client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
            client.Headers.Add("Content-Type", "application/json");
            responseString = client.UploadString(builder.Uri, postBody);
        }
        
        var response = JsonConvert.DeserializeObject<QnAMakerResult>(responseString);
        
        return response.Answer;
    }
    
    [LuisIntent("Greeting")]
    public async Task GreetingIntent(IDialogContext context, LuisResult result)
    {
        //await context.PostAsync($"Well, hello there.  How may I help you?"); //
        await context.SayAsync($"Well, hello there.  How may I help you?", $"Well, hello there.  How may I help you?");
        context.Wait(MessageReceived);
    }
    
    [LuisIntent("How You")]
    public async Task HowYouIntent(IDialogContext context, LuisResult result)
    {
        await context.SayAsync($"Doing very well, thank you!  How can I help?", $"Doing very well, thank you!  How can I help?"); //
        context.Wait(MessageReceived);
    }

    // Go to https://luis.ai and create a new intent, then train/publish your luis app.
    // Finally replace "MyIntent" with the name of your newly created intent in the following handler
    [LuisIntent("Diabetic Probability")]
    public async Task DiabeticProbabilityIntent(IDialogContext context, LuisResult result)
    {
        EntityRecommendation patientEntity;
        result.TryFindEntity("builtin.number", out patientEntity);
        this.patientNo = patientEntity.Entity;
        
        await context.SayAsync("OK, I'm going to need a little information about the patient","OK, I'm going to need a little information about the patient");
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
        
        await context.SayAsync($"Checking on probability of diabetes for patient {this.patientNo}.", $"Checking on probability of diabetes for patient {this.patientNo}."); //
        var mlResult = InvokeML(this.patientNo).Result;
        await context.SayAsync(mlResult, mlResult);
        
        context.Wait(MessageReceived);
    }
    
    private async Task<string> InvokeML(string patientNo)
    {
        string responseString = string.Empty;
        
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
                double mlValue = mlValues[0][1];
                
                //var mlValueDec = Double.Parse(mlValue);
                //var mlPerc = mlValueDec * 100.00;
                responseString = mlValue.ToString("#0.##%") + " Probability";
            }
            else
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                responseString = "Oh shucks, looks like we have an issue: " + response.StatusCode + ", " + responseContent;
            }
        }
        
        return responseString;
    }
    
    // Go to https://luis.ai and create a new intent, then train/publish your luis app.
    // Finally replace "MyIntent" with the name of your newly created intent in the following handler
    [LuisIntent("List Patients")]
    public async Task ListPatientsIntent(IDialogContext context, LuisResult result)
    {
        await context.SayAsync($"I've prepared your patient list for today.  Here you go... https://ucanalyticsdemodypgbf.blob.core.windows.net/static-content/PatientList.csv", $"I've prepared your patient list for today.  Here you go... https://ucanalyticsdemodypgbf.blob.core.windows.net/static-content/PatientList.csv"); //
        context.Wait(MessageReceived);
    }
}

private class QnAMakerResult
{
    /// <summary>
    /// The top answer found in the QnA Service.
    /// </summary>
    [JsonProperty(PropertyName = "answer")]
    public string Answer { get; set; }

    /// <summary>
    /// The score in range [0, 100] corresponding to the top answer found in the QnA    Service.
    /// </summary>
    [JsonProperty(PropertyName = "score")]
    public double Score { get; set; }
}

public class StringTable
{
    public string[] ColumnNames { get; set; }
    public string[,] Values { get; set; }
}