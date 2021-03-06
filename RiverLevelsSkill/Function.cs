using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace RiverLevelsSkill
{
    public class Function
    {
        private static readonly Uri ApiUrl = new Uri("http://api.rainchasers.com/v1");

        public SkillResponse Handler(SkillRequest request, ILambdaContext context)
        {
            var log = context.Logger;

            log.LogLine($"Skill Request:");
            log.LogLine(JsonConvert.SerializeObject(request));

            var response = Response(Resources().FirstOrDefault(), log, request);

            log.LogLine($"Skill Response:");
            log.LogLine(JsonConvert.SerializeObject(response));

            return response;
        }

        private static SkillResponse Response(RootResource resource, ILambdaLogger log, SkillRequest input)
        {
            if (input.GetRequestType() == typeof(LaunchRequest))
            {
                log.LogLine($"LaunchRequest");
                return Help(resource);
            }

            if (input.GetRequestType() == typeof(IntentRequest))
            {
                var request = (IntentRequest)input.Request;
                log.LogLine($"{request.Intent.Name}");
                switch (request.Intent.Name)
                {
                    case "AMAZON.CancelIntent":
                        return Stop(resource);
                    case "AMAZON.StopIntent":
                        return Stop(resource);
                    case "AMAZON.HelpIntent":
                        return Help(resource);

                    case "LevelIntent":
                        return Level(log, resource.River(request.Intent.Slots["river"].Value))
                            ?? Unknown(resource);
                }
            }

            log.LogLine($"Unknown command: {input.Request.Type}");
            return Help(resource);
        }

        private static SkillResponse Stop(RootResource resource)
        {
            return new SkillResponse
            {
                Version = "1.0",
                Response = new ResponseBody
                {
                    ShouldEndSession = true,
                    OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = resource.StopMessage
                    }
                }
            };
        }

        private static SkillResponse Help(RootResource resource)
        {
            return new SkillResponse
            {
                Version = "1.0",
                Response = new ResponseBody
                {
                    ShouldEndSession = false,
                    OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = resource.HelpMessage
                    }
                }
            };
        }

        private static SkillResponse Level(ILambdaLogger log, RiverResource resource)
        {
            if (resource == RiverResource.Unknown)
                return null;

            var client = new HttpClient();
            var requestUri = new Uri($"{ApiUrl}/river/{resource.Uuid}");

            log.LogLine($"Requesting: {requestUri}");

            var result = client.GetAsync(requestUri).Result;
            var content = result.Content.ReadAsStringAsync().Result;

            log.LogLine(content);

            var river = (dynamic)JsonConvert.DeserializeObject(content);

            return new SkillResponse
            {
                Version = "1.0",
                Response = new ResponseBody
                {
                    ShouldEndSession = true,
                    OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = $"The river {river.data.river}, {river.data.section} is {river.data.state.text}, {river.data.state.value}",
                    },
                }
            };
        }

        private static SkillResponse Unknown(RootResource resource)
        {
            return new SkillResponse
            {
                Version = "1.0",
                Response = new ResponseBody
                {
                    ShouldEndSession = true,
                    OutputSpeech = new PlainTextOutputSpeech
                    {
                        Text = resource.UnknownMessage
                    }
                }
            };
        }

        private static IEnumerable<RootResource> Resources()
        {
            yield return new RootResource("en-GB")
            {
                Description = "UK river levels",
                HelpMessage = "Ask me for the level of your favourite river",
                UnknownMessage = "Sorry, I don't know about that river",
                StopMessage = "See you on the river!",
                Rivers = new List<RiverResource>
                {
                    new RiverResource { Uuid = "4b50bd9e-9c88-4795-93e0-b1e5c213e9ed", Name = "Clough" },
                    new RiverResource { Uuid = "dd32f5a5-a507-4d87-b718-dd4adf9631dc", Name = "Crake" },
                    new RiverResource { Uuid = "75148ca0-ee5e-4344-8534-db9a59ed4cd0", Name = "Dee" },
                    new RiverResource { Uuid = "7ea17714-96c1-4e4f-a7df-a10b05251348", Name = "Lune" },
                    new RiverResource { Uuid = "9a417b1b-464e-4f49-be17-bbb38241e500", Name = "North Tyne" },
                    new RiverResource { Uuid = "60d6bb57-a6c5-43a2-8969-59d2cf8509c4", Name = "Rawthey" },
                    new RiverResource { Uuid = "3ac9af6d-df37-49a8-86f3-222e52744cc4", Name = "Ribble"},
                }
            };
        }
    }

    public class RootResource
    {
        public RootResource(string language)
        {
            Language = language;
        }

        public string Language { get; set; }
        public string Description { get; set; }
        public string HelpMessage { get; set; }
        public string UnknownMessage { get; set; }
        public string StopMessage { get; set; }
        public IEnumerable<RiverResource> Rivers { get; set; }

        public RiverResource River(string river)
        {
            return Rivers.SingleOrDefault(x => x.Name.ToLowerInvariant() == river.ToLowerInvariant())
                ?? RiverResource.Unknown;
        }
    }

    public class RiverResource
    {
        public static readonly RiverResource Unknown = new RiverResource { Name = "Unknown" };

        public string Name { get; set; }
        public string Uuid { get; set; }
    }
}
