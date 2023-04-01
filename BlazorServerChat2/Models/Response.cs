using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp15
{
    internal class Response
    {
        public string id { get; set; }
        public string @object { get; set; }

        public int created { get; set; }
        public string model { get; set; }
        public List<Choise> choices { get; set; }
        public Usage usage { get; set; }

    }

    internal class Choise
    {
        public int id { get; set; }
        public string finish_reason { get; set; }
        public Message message { get; set; }
    }

    internal class Usage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }

    internal class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}
