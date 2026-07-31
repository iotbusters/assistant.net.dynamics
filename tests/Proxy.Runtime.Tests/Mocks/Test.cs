using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assistant.Net.Dynamics.Proxy.Runtime.Tests.Mocks
{
    public class Test : ITest
    {
        public string Property => "1";
        public string Property2
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
        public string Property3 { get; set; } = "3";

        public string Function() => "2";
        public string Function(string a) => "3";
        public int Function(string a, int b) => 4;
        public T Method<T>(T a) where T : class, IEnumerable<char> => a;
        public Task Method() => Task.CompletedTask;
        public bool TryGet(string a, out string result)
        {
            result = a + "!";
            return true;
        }
        public event Action Event = delegate { };
    }
}