using System;
using System.Collections.Generic;
using System.Text;

namespace Blade
{
    public sealed class ResponseTaskResult<T>
    {
        internal ResponseTaskResult(Request request, Response response, T result)
        {
            Request = request;
            Response = response;
            Result = result;
        }

        public Request Request { get; private set; }
        public Response Response { get; private set; }
        public T Result { get; private set; }
    }
}
