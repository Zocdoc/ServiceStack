using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;
using ServiceStack.Common.Utils;
using ServiceStack.ServiceInterface.ServiceModel;
using ServiceStack.Text;

namespace ServiceStack.ServiceClient.Web
{
#if !NETFX_CORE && !WINDOWS_PHONE && !SILVERLIGHT
    [Serializable]
#endif
    public class WebServiceException
        : Exception
    {
        public WebServiceException() { }
        public WebServiceException(string message) : base(message) { }
        public WebServiceException(string message, Exception innerException) : base(message, innerException) { }
#if !NETFX_CORE && !WINDOWS_PHONE && !SILVERLIGHT
        public WebServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif

        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public object ResponseDto { get; set; }
        
        public string ResponseBody { get; set; }

        private string errorCode;

        private void ParseResponseDto()
        {
            string responseStatus;
            if (!TryGetResponseStatusFromResponseDto(out responseStatus))
            {
                if (!TryGetResponseStatusFromResponseBody(out responseStatus))
                {
                    errorCode = StatusDescription;
                    return;
                }
            }

            var rsMap = TypeSerializer.DeserializeFromString<Dictionary<string, string>>(responseStatus);
            if (rsMap == null) return;

            rsMap = new Dictionary<string, string>(rsMap, StringExtensions.InvariantComparerIgnoreCase());
            rsMap.TryGetValue("ErrorCode", out errorCode);
            rsMap.TryGetValue("Message", out errorMessage);
            rsMap.TryGetValue("StackTrace", out serverStackTrace);
            rsMap.TryGetValue("RequestTypeName", out requestTypeName);
            rsMap.TryGetValue("RequestContents", out requestContents);
        }

        private bool TryGetResponseStatusFromResponseDto(out string responseStatus)
        {
            responseStatus = String.Empty;
            try
            {
                if (ResponseDto == null)
                    return false;
                var jsv = TypeSerializer.SerializeToString(ResponseDto);
                var map = TypeSerializer.DeserializeFromString<Dictionary<string, string>>(jsv);
                map = new Dictionary<string, string>(map, StringExtensions.InvariantComparerIgnoreCase());

                return map.TryGetValue("ResponseStatus", out responseStatus);
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetResponseStatusFromResponseBody(out string responseStatus)
        {
            responseStatus = String.Empty;
            try
            {
                if (String.IsNullOrEmpty(ResponseBody)) return false;
                var map = TypeSerializer.DeserializeFromString<Dictionary<string, string>>(ResponseBody);
                map = new Dictionary<string, string>(map, StringExtensions.InvariantComparerIgnoreCase());
                return map.TryGetValue("ResponseStatus", out responseStatus);
            }
            catch
            {
                return false;
            }
        }

        public string ErrorCode
        {
            get
            {
                if (errorCode == null)
                {
                    ParseResponseDto();
                }
                return errorCode;
            }
        }

        private string errorMessage;
        public string ErrorMessage
        {
            get
            {
                if (errorMessage == null)
                {
                    ParseResponseDto();
                }
                return errorMessage;
            }
        }

        private string serverStackTrace;
        public string ServerStackTrace
        {
            get
            {
                if (serverStackTrace == null)
                {
                    ParseResponseDto();
                }
                return serverStackTrace;
            }
        }

        private string requestTypeName;
        public string RequestTypeName
        {
            get
            {
                if (requestTypeName == null)
                {
                    ParseResponseDto();
                }
                return requestTypeName;
            }
        }

        private string requestContents;
        public string RequestContents
        {
            get
            {
                if (requestContents == null)
                {
                    ParseResponseDto();
                }
                return requestContents;
            }
        }

        public ResponseStatus ResponseStatus
        {
            get
            {
                if (this.ResponseDto == null)
                    return null;

                var hasResponseStatus = this.ResponseDto as IHasResponseStatus;
                if (hasResponseStatus != null)
                    return hasResponseStatus.ResponseStatus;

                var propertyInfo = this.ResponseDto.GetType().GetPropertyInfo("ResponseStatus");
                if (propertyInfo == null)
                    return null;

                return ReflectionUtils.GetProperty(this.ResponseDto, propertyInfo) as ResponseStatus;
            }
        }

        public List<ResponseError> GetFieldErrors()
        {
            var responseStatus = ResponseStatus;
            if (responseStatus != null)
                return responseStatus.Errors ?? new List<ResponseError>();

            return new List<ResponseError>();
        }
    }
}
