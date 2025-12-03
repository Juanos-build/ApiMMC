using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Logging;
using ApiMMC.Services.Helpers.Settings;

namespace ApiMMC.Services.Helpers.Filters
{
    public class ResponseHelper(AppSettings appSettings)
    {
        private readonly AppSettings _appSettings = appSettings;

        public void Info(string message)
        {
            LoggerManager.logger.Info(message);
        }

        public void Error(Exception ex, string message, object request = null)
        {
            LoggerManager.logger
                .WithProperty("eventType", "BusinessError")
                .WithProperty("errorCode", _appSettings.ErrorResult)
                .WithProperty("exception", ex.Message)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("source", ex.Source)
                .WithProperty("request", request)
                .Error(message);
        }

        public void Warn(string message, object request = null)
        {
            LoggerManager.logger
                .WithProperty("eventType", "BusinessError")
                .WithProperty("errorCode", _appSettings.ErrorResult)
                .WithProperty("request", request)
                .Warn(message);
        }

        public Response<T> Error<T>(Response<T> response, ResultException ex, object request = null)
        {
            response.StatusMessage = ex.Message;
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode = ex.ErrorCode ?? _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "ResultException")
                .WithProperty("errorCode", ex.ErrorCode)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("source", ex.Source)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Error<T>(Response<T> response, DataAccessException ex, object request = null)
        {
            response.StatusMessage = "Error de acceso a datos: " + ex.Message;
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode = ex.ErrorCode ?? _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "DataError")
                .WithProperty("errorCode", ex.ErrorCode)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("source", ex.Source)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Error<T>(Response<T> response, BusinessException ex, object request = null)
        {
            response.StatusMessage = ex.Message;
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode = ex.ErrorCode ?? _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "BusinessError")
                .WithProperty("errorCode", ex.ErrorCode)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Exception<T>(Response<T> response, TechnicalException ex, object request = null)
        {
            response.StatusMessage = ex.Message;
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode = ex.ErrorCode ?? _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "TechnicalException")
                .WithProperty("errorCode", ex.ErrorCode)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Exception<T>(Response<T> response, UnexpectedException ex, object request = null)
        {
            response.StatusMessage = $"{ex.Message} (Error inesperado) {(ex.InnerException != null ? "InnerException: " + ex.InnerException?.Message : string.Empty)}";
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode = ex.ErrorCode ?? _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "UnexpectedError")
                .WithProperty("errorCode", ex.ErrorCode)
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("source", ex.Source)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Exception<T>(Response<T> response, Exception ex, object request = null)
        {
            response.StatusMessage = $"{ex.Message} {(ex.InnerException != null ? "InnerException: " + ex.InnerException?.Message : string.Empty)}";
            response.MessageDetail = ex.InnerException?.Message;
            response.StatusCode ??= _appSettings.ErrorResult;

            LoggerManager.logger
                .WithProperty("eventType", "Error")
                .WithProperty("innerException", ex.InnerException?.Message)
                .WithProperty("source", ex.Source)
                .WithProperty("request", request ?? null)
                .Error(ex.Message);

            return response;
        }

        public Response<T> Success<T>(Response<T> response, object request = null, Response<T> result = null)
        {
            ArgumentNullException.ThrowIfNull(response);

            response.Result = result != null ? result.Result : response.Result;
            response.StatusMessage = result?.StatusMessage ?? response.StatusMessage ?? "Proceso completado correctamente - OK";
            response.StatusCode ??= _appSettings.ResponseResult;
            response.IsSuccess = true;

            LoggerManager.logger
                .WithProperty("eventType", "Success")
                .WithProperty("request", request)
                .WithProperty("response", response)
                .Info(response.StatusMessage);

            return response;
        }
    }
}
