using CoreLayer.CrossCuttingConcerns.Logging.Log4Net;
using CoreLayer.CrossCuttingConcerns.Logging.Log4Net.Loggers;
using CoreLayer.Extensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Core.Extensions
{
    public class ExceptionMiddleware
    {
        private RequestDelegate _next;

        private LoggerServiceBase _databaseLoggerServiceBase;

        private LoggerServiceBase _fileLoggerServiceBase;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;

            _databaseLoggerServiceBase = (LoggerServiceBase)Activator.CreateInstance(typeof(DatabaseLogger));

            _fileLoggerServiceBase = (LoggerServiceBase)Activator.CreateInstance(typeof(FileLogger));
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (OperationCanceledException)
            {
                // 1. İptal durumunda log kirliliği yapmıyoruz. 
                _fileLoggerServiceBase.Info($"İşlem kullanıcı tarafından iptal edildi: {httpContext.Request.Path}");

                // Bağlantı koptuğu için Redirect veya ağır modellerle uğraşmıyoruz.
                httpContext.Response.StatusCode = 499;
            }
            catch (ValidationException validationException)
            {
                HandleValidationException(validationException, httpContext);
            }
            catch (Exception exception)
            {
                HandleGenericException(exception, httpContext);
            }
        }

        private void HandleValidationException(ValidationException exception, HttpContext httpContext)
        {
            ValidationErrorDetail exceptionModel = new()
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Message = exception.Message,
                ValidationErrors = exception.Errors
            };

            _databaseLoggerServiceBase.Error(exceptionModel);
            _fileLoggerServiceBase.Error(exceptionModel);

            // Header gönderilmediyse yönlendir
            if (!httpContext.Response.HasStarted)
                httpContext.Response.Redirect(httpContext.Request.Path.ToString());
        }

        private void HandleGenericException(Exception exception, HttpContext httpContext)
        {
            _databaseLoggerServiceBase.Error(exception);
            _fileLoggerServiceBase.Error(exception);

            if (!httpContext.Response.HasStarted)
                httpContext.Response.Redirect("/ErrorPage/Error404");
        }
    }
}
