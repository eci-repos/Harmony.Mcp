using Harmony.Mcp.Models;
using System.Text.Json;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Client;

public class McpHelper
{
   public const string EmbeddingsToolName = "embeddings.embed";
   public const string SimilarityToolName = "semantic.similarity";
   public const string ChatCompletionToolName = "chat.completions";
   public const string WorkflowRunToolName = "workflow.run";

   public static McpToolDescriptor? FindEmbeddingsTool(
      IReadOnlyList<McpToolDescriptor>? tools = null) =>
      (tools!.FirstOrDefault(t => t.Name == EmbeddingsToolName));
   public static McpToolDescriptor? FindSemanticSimilarityTool(
      IReadOnlyList<McpToolDescriptor>? tools = null) =>
      (tools!.FirstOrDefault(t => t.Name == SimilarityToolName));
   public static McpToolDescriptor? FindChatCompletionsTool(
      IReadOnlyList<McpToolDescriptor>? tools = null) =>
      (tools!.FirstOrDefault(t => t.Name == ChatCompletionToolName));
   public static McpToolDescriptor? FindWorkflowRunTool(
      IReadOnlyList<McpToolDescriptor>? tools = null) =>
      (tools!.FirstOrDefault(t => t.Name == WorkflowRunToolName));

   public static Task<McpEmbeddingsResponse> EmbeddingsAsync(
      McpClient client, EmbeddingsArgs args, McpToolDescriptor? schema = null) =>
      McpTyped.CallAsync<EmbeddingsArgs, McpEmbeddingsResponse>(
         client, EmbeddingsToolName, args, schema);

   public static Task<McpSimilarityResponse> SemanticSimilarityAsync(
      McpClient client, McpSimilarityArgs args, McpToolDescriptor? schema = null) =>
      McpTyped.CallAsync<McpSimilarityArgs, McpSimilarityResponse>(
         client, SimilarityToolName, args, schema);

   public static Task<McpChatCompletionResponse> ChatAsync(
      McpClient client, JsonElement args, McpToolDescriptor? schema = null) =>
      McpTyped.CallAsync<JsonElement, McpChatCompletionResponse>(
         client, ChatCompletionToolName, args, schema);
}
