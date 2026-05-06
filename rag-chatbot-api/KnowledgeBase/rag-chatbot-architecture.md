# RAG Chatbot Architecture

The solution has two applications:

- Angular frontend in rag-chatbot
- ASP.NET Core API in rag-chatbot-api

Frontend sends authenticated requests to the API using JWT Bearer tokens.
The chat screen calls POST /api/rag/query.

The RAG controller uses a Semantic Kernel-based retrieval-augmentation-generation flow:

1. Load indexed knowledge documents from the vector table.
2. Generate a query embedding with the configured embedding model.
3. Rank indexed documents by vector similarity.
4. Generate an answer with the configured OpenAI-compatible chat completion endpoint through Semantic Kernel.

If Semantic Kernel configuration is incomplete or the AI server request fails, the API returns a configuration or request failure message instead of a legacy retrieval-only fallback.
