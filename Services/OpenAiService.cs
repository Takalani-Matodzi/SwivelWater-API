#pragma warning disable OPENAI001

using Microsoft.Extensions.Options;
using OpenAI.Responses;
using SwivelWater.API.Models;

namespace SwivelWater.API.Services;

public class OpenAiService : IAiService
{
    private readonly AiSettings _settings;
    private readonly ResponsesClient _client;

    public OpenAiService(IOptions<AiSettings> settings)
    {
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured."
            );
        }

        _client = new ResponsesClient(_settings.ApiKey);
    }

    public async Task<string> GetResponseAsync(
        string message,
        string accessLevel,
        string? userId = null,
        string? trustedContext = null)
    {
        var systemPrompt = BuildSystemPrompt(
            accessLevel,
            userId
        );

        var options = new CreateResponseOptions
        {
            Model = _settings.Model
        };

        var contextSection = string.IsNullOrWhiteSpace(trustedContext)
            ? "No private backend data was provided for this question."
            : trustedContext;

        options.InputItems.Add(
            ResponseItem.CreateUserMessageItem(
                $"""
                {systemPrompt}

                TRUSTED BACKEND CONTEXT:
                {contextSection}

                USER QUESTION:
                {message}

                Answer using the trusted backend context when relevant.

                Do not invent database information.

                Never claim to know private information that was not
                supplied in the trusted backend context.
                """
            )
        );

        var response = await _client.CreateResponseAsync(options);

        return response.Value.GetOutputText();
    }

    private static string BuildSystemPrompt(
        string accessLevel,
        string? userId)
    {
        const string currencyRule = """
            Swivel Water operates in South Africa.

            All monetary values must be presented in South African Rand.

            Use the "R" symbol and two decimal places where appropriate.
            Examples:
            R35.00
            R70.00
            R1,250.00

            Never use the "$" symbol, USD, dollars, or another currency
            when describing Swivel Water prices, payments, order totals,
            sales, revenue, or any other monetary value.
            """;

        return accessLevel switch
        {
            "PUBLIC" => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                You are speaking to a public visitor who is not logged in.

                You may answer general questions about Swivel Water,
                its products, services, ordering process, delivery process,
                account registration, and general customer support information.

                Do not provide private customer, employee, driver, payment,
                order, delivery, or administrative information.

                Do not invent company policies or private database information.

                If trusted backend information is provided, use it when
                answering the user's question.
                """,

            "CUSTOMER" => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                The current authenticated user is a CUSTOMER.

                User ID:
                {userId}

                The customer may ask about public Swivel Water information
                and information belonging to their own account.

                Never reveal another customer's private information.

                Never assume access to employee-only or admin-only information.

                Do not invent order, payment, delivery, or account information.

                Only use customer-specific information supplied by the
                trusted backend context.
                """,

            "EMPLOYEE" => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                The current authenticated user is an EMPLOYEE.

                User ID:
                {userId}

                Answer public questions and authorized operational questions.

                Do not reveal private customer information unless the backend
                explicitly provides that information for the requested operation.

                Do not provide admin-only management information.

                Do not invent operational data.

                Only use employee-specific information supplied by the
                trusted backend context.
                """,

            "DRIVER" => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                The current authenticated user is a DRIVER.

                User ID:
                {userId}

                Answer public questions and questions related to the driver's
                own assigned delivery operations.

                Do not reveal unrelated customer, employee, payment,
                inventory-management, or administrative information.

                Do not invent delivery information.

                Only use driver-specific information supplied by the
                trusted backend context.
                """,

            "ADMIN" => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                The current authenticated user is an ADMIN / MANAGER.

                User ID:
                {userId}

                The administrator may access authorized management information.

                Still do not invent database values or claim that an operation
                occurred unless the backend actually confirms it.

                Only use management information supplied by the
                trusted backend context.
                """,

            _ => $"""
                You are the Swivel Water AI assistant.

                {currencyRule}

                Provide only general public information.

                Do not provide private customer, employee, driver,
                payment, order, delivery, or administrative information.
                """
        };
    }
}

#pragma warning restore OPENAI001