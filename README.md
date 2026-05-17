# Smart Home Repair Multi-Agent System

## Overview
An Enterprise-grade, AI-driven Smart Home Repair API developed using ASP.NET Core and Microsoft Semantic Kernel. The system utilizes a Multi-Agent Architecture (Orchestrator-Worker pattern) to autonomously analyze physical damage via image processing, determine issue severity, and execute complex workflows ranging from DIY guidance to professional technician scheduling.

## Enterprise Architecture & Design Patterns
* **Agentic Orchestration (Router Pattern):** Implemented a `RepairOrchestratorAgent` acting as a central decision-maker. It analyzes multimodal inputs (text + images) and deterministically delegates tasks to specialized sub-agents based on strict predefined workflows (DIY vs. Professional), completely eliminating AI hallucination in critical paths.
* **Stateful Session Management (Thread Isolation):** Designed a `SessionStore` utilizing `IMemoryCache` to manage `ChatHistoryAgentThread` objects per user. This ensures 100% Session Isolation and Thread-Safety in a concurrent HTTP environment, preventing data leaks across different user requests.
* **Agent-as-a-Tool (Facade Pattern):** Encapsulated specific domains (Tool Recommendation, Step-by-Step Guidance, Cost Estimation, and Scheduling) into isolated `ChatCompletionAgent` microservices. These sub-agents are exposed as strictly defined tools `[KernelFunction]` via Dependency Injection.
* **Retrieval-Augmented Generation (RAG):** Integrated Qdrant Vector Database to chunk, embed, and retrieve context from uploaded PDF manuals, providing grounded, context-aware responses.
* **Deterministic Outputs:** Enforced strict JSON object generation (`ResponseFormat = "json_object"`) with `Temperature = 0` to ensure API responses are predictable, structured, and ready for frontend integration.
* **Memory Optimization:** Utilized `ChatHistoryTruncationReducer` to prevent context window bloat and manage LLM token limits efficiently.

## Tech Stack
* **Framework:** ASP.NET Core Web API, .NET 10, C#
* **AI Orchestration:** Microsoft Semantic Kernel
* **LLM Models:** OpenAI GPT-4o-mini (Chat & Vision), text-embedding-3-small
* **Vector Database:** Qdrant (Self-hosted/Local)
* **Document Parsing:** PdfPig, Semantic Kernel TextChunker
* **State Management:** In-Memory Caching (`IMemoryCache`)

## Core API Endpoints
* `POST /Home/analyze-multi`: The primary Multi-Agent endpoint. Accepts a user prompt, a unique `thread_id`, and an optional `imageUrl`. Analyzes the issue and orchestrates the Sub-Agents to return a structured JSON response (Tools/Steps for DIY, or Cost/Appointment for Professional).
* `POST /Home/upload-pdf`: Parses PDF manuals, splits them into semantic chunks, generates vector embeddings, and upserts them into the Qdrant Vector Store.
* `POST /Home/ask-pdf`: Performs a similarity search on the Vector DB and answers questions based strictly on the retrieved document context.
* `POST /Home/analyze-reduced`: A single-agent analysis endpoint implementing a Sliding Window approach (`ChatHistoryTruncationReducer`) to limit history size.
* `POST /Home/clear-session`: Safely disposes of a specific user's isolated chat history from the cache.
