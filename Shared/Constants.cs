using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketingTest.Shared
{
    public static class Constants
    {
        // Базові константи Dataverse
        public const string Target = "Target";
        public const string Payload = "payload";
        public const string Response = "response";

        // Стандартне повідомлення про помилку згідно з гайдлайном компанії
        public const string UnexpectedErrorMassage =
            "An unexpected error occurred. Please contact your system administrator.";
    }
}