// Copyright (c) 2025 SignalWire
//
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Configuration options for a BedrockAgent.

using SignalWire.Agent;

namespace SignalWire.Agents;

/// <summary>Configuration options for a <see cref="BedrockAgent"/>.</summary>
public sealed class BedrockOptions
{
    /// <summary>Agent name (default "bedrock_agent").</summary>
    public string Name { get; init; } = "bedrock_agent";

    /// <summary>HTTP route for the agent (default "/bedrock").</summary>
    public string Route { get; init; } = "/bedrock";

    /// <summary>Initial prompt; can be overridden later with SetPromptText.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Bedrock voice id (default "matthew").</summary>
    public string VoiceId { get; init; } = "matthew";

    /// <summary>Generation temperature (0-1, default 0.7).</summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>Nucleus sampling parameter (0-1, default 0.9).</summary>
    public double TopP { get; init; } = 0.9;

    /// <summary>Maximum tokens to generate (default 1024).</summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>Basic-auth user forwarded to <see cref="AgentOptions"/>.</summary>
    public string? BasicAuthUser { get; init; }

    /// <summary>Basic-auth password forwarded to <see cref="AgentOptions"/>.</summary>
    public string? BasicAuthPassword { get; init; }
}
