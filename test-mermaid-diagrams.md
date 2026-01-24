# Mermaid Diagram Types Test

This document tests various Mermaid diagram types to verify support.

## Graph TB (Top to Bottom)

```mermaid
graph TB
    A[Start] --> B{Decision}
    B -->|Yes| C[Action 1]
    B -->|No| D[Action 2]
    C --> E[End]
    D --> E
```

## Graph LR (Left to Right)

```mermaid
graph LR
    A[Client] --> B[API Gateway]
    B --> C[Auth Service]
    B --> D[Business Logic]
    D --> E[Database]
```

## Sequence Diagram

```mermaid
sequenceDiagram
    participant User
    participant Frontend
    participant Backend
    participant Database
    
    User->>Frontend: Login Request
    Frontend->>Backend: Authenticate
    Backend->>Database: Query User
    Database-->>Backend: User Data
    Backend-->>Frontend: Auth Token
    Frontend-->>User: Login Success
```

## Flowchart (Modern Syntax)

```mermaid
flowchart TD
    A[Start] --> B{Is it working?}
    B -->|Yes| C[Great!]
    B -->|No| D[Debug]
    D --> A
```

## Azure DevOps Colon Syntax

::: mermaid
graph TB
    Start --> End
:::
