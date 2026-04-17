using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

namespace Minedu.VC.Issuer.Models
{
    public static class SchemaValidator
    {
        public static bool Validate(string json, string schemaPath)
        {
            var schema = JSchema.Parse(System.IO.File.ReadAllText(schemaPath));
            var jObject = JObject.Parse(json);

            return jObject.IsValid(schema, out IList<string> errors);
        }
    }
}
