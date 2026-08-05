using Newtonsoft.Json;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Scanner.Services
{
    public class WorkOrderRemarkService
    {
        private const string BaseUrl =
            "https://repair-rms.vercel.app";

        private const string RemarksPath =
            "/api/v1/work-orders/remarks";

        private static readonly HttpClient HttpClient =
            CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12;

            HttpClient client =
                new HttpClient();

            client.BaseAddress =
                new Uri(BaseUrl);

            client.Timeout =
                TimeSpan.FromSeconds(30);

            return client;
        }

        public async Task<WorkOrderRemarkResponse>
            GetRemarksAsync(
                string token,
                long? startTimestamp,
                int limit)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException(
                    "没有登录 Token，请重新登录。"
                );
            }

            if (limit < 1)
            {
                limit = 1;
            }

            if (limit > 200)
            {
                limit = 200;
            }

            long effectiveStartTimestamp =
                startTimestamp.HasValue
                    ? startTimestamp.Value
                    : GetCurrentMonthStartTimestamp();

            string requestUrl =
                RemarksPath
                + "?start_timestamp="
                + effectiveStartTimestamp
                    .ToString(
                        CultureInfo.InvariantCulture
                    )
                + "&limit="
                + limit.ToString(
                    CultureInfo.InvariantCulture
                );

            using (
                HttpRequestMessage request =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        requestUrl
                    )
            )
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        token
                    );

                request.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"
                    )
                );

                HttpResponseMessage response;

                try
                {
                    response =
                        await HttpClient.SendAsync(
                            request
                        );
                }
                catch (TaskCanceledException)
                {
                    throw new InvalidOperationException(
                        "获取工单备注超时，请检查网络。"
                    );
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(
                        "无法连接服务器："
                        + ex.Message
                    );
                }

                string responseJson =
                    await response.Content
                        .ReadAsStringAsync();

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException(
                        ReadErrorMessage(
                            responseJson,
                            "Token 已过期，请重新登录。"
                        )
                    );
                }

                if (response.StatusCode ==
                    HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException(
                        ReadErrorMessage(
                            responseJson,
                            "当前账号没有查看工单备注的权限。"
                        )
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        ReadErrorMessage(
                            responseJson,
                            "获取工单备注失败，HTTP "
                            + ((int)response.StatusCode)
                                .ToString()
                        )
                    );
                }

                WorkOrderRemarkResponse result;

                try
                {
                    result =
                        JsonConvert.DeserializeObject
                            <WorkOrderRemarkResponse>(
                                responseJson
                            );
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        "工单接口返回格式无法解析："
                        + ex.Message
                    );
                }

                if (result == null)
                {
                    throw new InvalidOperationException(
                        "服务器返回了空响应。"
                    );
                }

                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        "服务器返回 success=false。"
                    );
                }

                if (result.WorkOrders == null)
                {
                    result.WorkOrders =
                        new List<WorkOrderRemark>();
                }

                return result;
            }
        }

        public Task<WorkOrderRemarkResponse>
            GetRemarksAsync(string token)
        {
            return GetRemarksAsync(
                token,
                null,
                200
            );
        }

        private static long
            GetCurrentMonthStartTimestamp()
        {
            /*
             * Windows 系统中的美东时区名称。
             */
            TimeZoneInfo easternTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "Eastern Standard Time"
                );

            DateTime utcNow =
                DateTime.UtcNow;

            DateTime easternNow =
                TimeZoneInfo.ConvertTimeFromUtc(
                    utcNow,
                    easternTimeZone
                );

            DateTime easternMonthStart =
                new DateTime(
                    easternNow.Year,
                    easternNow.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Unspecified
                );

            DateTime utcMonthStart =
                TimeZoneInfo.ConvertTimeToUtc(
                    easternMonthStart,
                    easternTimeZone
                );

            DateTimeOffset offset =
                new DateTimeOffset(
                    utcMonthStart,
                    TimeSpan.Zero
                );

            return offset.ToUnixTimeSeconds();
        }

        private static string ReadErrorMessage(
            string json,
            string defaultMessage)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultMessage;
            }

            try
            {
                ApiErrorResponse errorResponse =
                    JsonConvert.DeserializeObject
                        <ApiErrorResponse>(json);

                if (errorResponse != null &&
                    errorResponse.Error != null &&
                    !string.IsNullOrWhiteSpace(
                        errorResponse.Error.Message
                    ))
                {
                    return errorResponse.Error.Message;
                }
            }
            catch
            {
                // 忽略错误响应解析异常。
            }

            return defaultMessage;
        }
    }
}