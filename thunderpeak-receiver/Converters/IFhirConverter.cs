using System;
using System.Collections.Generic;
using System.Text;

namespace Cdc.Surveillance.Converters
{
    public interface IFhirConverter<T>
    {
        public T Convert(string rawMessage, string processId);
    }
}
