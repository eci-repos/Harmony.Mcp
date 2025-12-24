# Model Context Protocol (MCP) Library

## MCP Defined

The Model Context Protocol (MCP) is an open standard designed to let AI assistants (like ChatGPT or Claude) securely interact with external tools, data sources, and workflows in a structured way. It uses JSON-RPC 2.0 as the messaging layer and defines a set of capabilities—such as listing tools, calling tools, and streaming responses—that make it easy for clients and servers to communicate regardless of the underlying transport.

At its core, MCP enables a client (usually an AI model or agent) to discover and invoke “tools” exposed by a server. These tools can be anything from database queries and semantic search to workflow orchestration or file operations. The protocol is transport-agnostic, meaning it can run over stdio (most common for local integrations), HTTP/SSE, or even WebSocket for remote or multi-client scenarios. This flexibility allows MCP to support both local desktop apps and distributed systems while maintaining a consistent interface for tool invocation and result handling.

The main benefits of MCP are security, interoperability, and extensibility. By using a standardized schema for requests and responses, it reduces the complexity of integrating AI with external systems. It also provides a clear separation of concerns: the AI focuses on reasoning and language, while the MCP server handles domain-specific logic and data access. This makes MCP a key building block for building safe, modular, and powerful AI-driven applications.

## MCP Library Transport Options
In an MCP (Model Context Protocol) solution, multiple transport options can be used to connect clients and servers. The main ones are stdio, TCP, and Named Pipes. All of these carry the same JSON-RPC 2.0 messages with MCP framing (using Content-Length headers); the only difference is the physical I/O channel.

TCP is a natural fit for scenarios where client and server run as separate processes, potentially on different machines. It’s easy to debug with external tools and works cross-platform. Named Pipes offer a simple, efficient IPC mechanism on Windows (and Unix domain sockets on Linux/macOS). They’re ideal for same-machine communication without needing a network port, and they allow multiple clients to connect to the same server concurrently.

MCP’s stdio transport is preferred because it’s the simplest, most secure, and most compatible option for local integrations. It avoids network complexity—no ports, firewalls, or TLS—and works across platforms without extra dependencies. Since it uses the process’s standard input/output streams, there’s no exposed attack surface, making it inherently safer for desktop or CLI environments.

Additionally, stdio is the baseline transport in the MCP specification, ensuring maximum interoperability with all MCP clients like ChatGPT or Claude Desktop. It offers low latency, deterministic framing, and is ideal for single-client scenarios. Alternative transports like HTTP or WebSocket are useful for remote or multi-client setups, but for local tools, stdio remains the most reliable and spec-compliant choice.

## MCP Library Overview
The MCP client and server implementation demonstrates the core communication flow of the **Model Context Protocol**, using a shared library to handle both roles in a single executable. The **server** sets up a registry of tools—such as `embeddings.embed`, `semantic.similarity`, `chat.completions`, and `workflow.run`—and exposes them over a chosen transport (stdio, TCP, or named pipes). It listens for JSON-RPC 2.0 messages, processes them, and responds with framed JSON messages. The **client**, on the other hand, knows how to connect to the server, send `initialize`, list tools, and invoke these tools using the same transport framing.

By supporting **multiple transports** in one codebase, this example shows how MCP is not tied to a particular deployment style. You can run client and server in the same process (via in-proc pipes), in separate processes on the same machine (via stdio or pipes), or even across machines (via TCP). The transport abstraction layer (`IMcpTransport`) cleanly separates connection logic from protocol logic, making it easy to extend or swap transports without changing how messages are built or parsed.

Understanding this example gives you a **solid foundation** for implementing more complex MCP scenarios. Once you grasp how to set up a minimal client–server handshake, route tool calls, and use proper framing, you can build richer capabilities such as streaming responses, long-lived workflows, multi-client broadcasting, or editor integrations. This pattern mirrors what real MCP-based systems (and LSP servers) do under the hood: start simple with stdio communication, then scale up to more advanced transports, tools, and session management as the application grows.

## Why Implement an MCP?

Placing services behind an MCP server provides structure, safety, and composability to an AI or tool-driven system. The Model Context Protocol acts as a unifying interface between intelligent agents and the services they use—standardizing how tools, workflows, and data sources are exposed. Rather than embedding dozens of APIs directly into every client or model plugin, the MCP server becomes a single, well-defined gateway. Each capability—whether it’s file access, chat completion, or workflow orchestration—is registered as a tool the client can call over JSON-RPC, giving a consistent way to discover, invoke, and handle results.

This design also enforces clear separation of concerns. Clients (or models) focus on reasoning, while the MCP server handles the mechanical details of execution—network calls, filesystem access, embeddings, or prompt orchestration through Semantic Kernel. Because communication is message-based and transport-agnostic, services can be added or replaced without changing the client’s logic. For example, a semantic.similarity tool might first run locally, but later delegate to a remote vector database without the client ever needing to know. This flexibility makes MCP servers an ideal foundation for evolving systems that mix AI reasoning with deterministic computation.

Beyond modularity, an MCP server provides security and governance benefits. By isolating tools behind the server boundary, you can strictly control what an AI model can do—what files it may read, what APIs it can hit, and which workflows it can trigger. Logging, authentication, and rate-limiting can all live in the server layer, providing observability and safety for actions that would otherwise be opaque.

Finally, putting these recommended services behind MCP turns your environment into a composable ecosystem rather than a tangle of special-purpose integrations. The same client can call `embeddings.embed`, `semantic.similarity`, `chat.completions`, and `workflow.run` using one protocol, and different agents—or even different LLMs—can reuse the same endpoints. Understanding and building this modular structure in a small example prepares you for far more complex scenarios, such as multi-agent coordination, tool-augmented reasoning, or dynamic orchestration across distributed systems.

### Don't Expose Everthing as a Tool

In an MCP solution, resources complement tools by providing structured, discoverable data endpoints that clients can read without invoking custom RPC calls. They’re ideal for stable or frequently accessed information such as files, embeddings, configurations, prompts, or metrics.

Using resource URIs (like file://, vector://, prompt://, or policy://), clients can easily discover, cache, and retrieve data. This keeps reads lightweight and predictable, while actions and mutations remain explicit through tools (like chat.complete or workflow.run).

This separation between tools (actions) and resources (data) results in a cleaner, safer, and more scalable design. It allows you to centralize governance and documentation, simplify integrations, and make your MCP server extensible—ideal for complex AI workflows, multi-agent systems, or dynamic application environments.

#### Prompts
Prompts are a great example of resources in MCP. By exposing prompts as resource URIs (e.g., prompt://my-prompt), clients can fetch and cache them without needing custom tool calls. This allows for easy versioning, reuse, and sharing of prompt templates across different agents or workflows.

When a client needs to generate text, it can retrieve the prompt template from the resource endpoint, fill in any variables, and then pass the completed prompt to a chat.completions tool. This keeps the prompt management separate from the completion logic, making it easier to update prompts without changing tool implementations.

In practical MCP implementations, prompts are managed as reusable, versioned resources rather than hardcoded text. By publishing them through the server as discoverable templates (prompt://...), teams can standardize, update, and govern how AI models are instructed without changing client code. This promotes consistency, maintainability, and safe reuse across workflows—ensuring that improvements or policy updates to prompts automatically benefit all connected clients while keeping prompt logic centralized and composable within the MCP ecosystem.

#### Policies

Policies are another important resource type in MCP. By exposing policies as resource URIs (e.g., policy://data-access), clients can retrieve and enforce governance rules without needing custom tool calls. This allows for centralized management of access controls, usage limits, and compliance requirements.

When a client needs to perform an action, it can first fetch the relevant policy from the resource endpoint to determine what is allowed. This keeps policy enforcement separate from tool implementations, making it easier to update policies without changing client logic.

##### How to work with Policies in MCP

When an MCP client, such as an AI assistant, connects to the server, it first sends a resources/list request to see what resources are available. Among the results, it finds an entry named policy://content-filter/v1, described as the Content Safety and Usage Policy. The descriptor explains that this policy defines filtering and moderation rules for AI-generated text and tool usage, applies to tools like chat.complete and workflow.run, and was last updated by the MCP Governance Team.

To learn what the policy actually contains, the client sends a resources/read request for that URI. The server replies with the full policy content, which includes several safety rules:

```
{
  "schemaVersion": "2024-11-01",
  "type": "policy",
  "rules": [
    {
      "id": "safety.001",
      "description": "Block generation of hateful, harassing, or violent content.",
      "appliesTo": ["chat.complete", "workflow.run"],
      "action": "block"
    },
    {
      "id": "safety.002",
      "description": "Prevent access to restricted files or system commands.",
      "appliesTo": ["fs.read", "fs.write", "process.start"],
      "action": "deny"
    },
    {
      "id": "safety.003",
      "description": "Require user confirmation before executing external API calls.",
      "appliesTo": ["http.get", "web.search"],
      "action": "prompt"
    }
  ],
  "enforcement": {
    "defaultAction": "log",
    "reporting": {
      "enabled": true,
      "endpoint": "audit://policy/events"
    }
  }
}
```

Once retrieved, the client caches this policy and uses it to guide its behavior.
For example, if a user asks the AI to summarize controversial content, the client checks rule safety.001. Since it blocks hateful or violent material, the client safely declines the request and logs the action under the policy’s audit://policy/events endpoint.
Later, if the user asks it to run a web search for sensitive data, rule safety.003 triggers — the client pauses and requests user confirmation before performing web.search. If the user approves, it proceeds; if not, it stops and logs the attempt.

This simple flow — list → read → enforce — shows how MCP policies make governance transparent and enforceable. The server publishes the safety rules once, and all clients connected to it follow the same trusted, machine-readable policy without extra code or configuration. It’s a practical way to keep AI tools consistent, secure, and responsible.

## From this MCP and SK Library to Real-World Solutions

The developed MCP client and server, combined with the Semantic Kernel (SK) library, form a solid foundation for building enterprise-grade AI orchestration solutions. Together, they provide a structured and extensible environment where large language models, workflows, and business logic can interact safely through a unified protocol. By exposing tools, resources, and policies via the MCP framework, enterprises can modularize their AI capabilities—such as content generation, summarization, or document processing—into discoverable, auditable, and governable services. The clear separation between reasoning (client) and execution (server) aligns perfectly with corporate security and compliance needs, ensuring that sensitive operations like data access or API calls remain under strict policy control.

This architecture also scales naturally. Teams can deploy the MCP server as a microservice or containerized backend, connect multiple clients (agents, chat interfaces, or automated pipelines), and manage complex Semantic Kernel workflows that span data retrieval, prompt chaining, and model orchestration. Because the system is built on open standards—JSON-RPC, stdio, or TCP—it integrates cleanly with existing enterprise infrastructure and CI/CD processes. In essence, this implementation is not just a demo—it’s a blueprint for production-ready AI platforms, enabling organizations to grow from prototypes into robust, compliant, and context-aware intelligent systems.

## Contributing

Contributions to the MCP & SK Libraries are welcome! If you have ideas for new features, bug fixes, or improvements, please open an issue or submit a pull request on the GitHub repository. Make sure to follow the existing coding style and include tests for any new functionality. Your contributions help make the MCP / SK ecosystem stronger and more versatile for everyone.



