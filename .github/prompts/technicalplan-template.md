---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [<Feature Name>] <Component Name> - Title"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT
**Introduction:** > *Describe the business value and the "Why". What problem are we solving?*

**Endpoint & Inputs:**
* **Route:** (e.g., `POST /v1/orders`)
* **Payload:** (e.g., `CreateOrderRequest` DTO)
* **Auth/Permissions:** (e.g., `Scope: orders.write`)

**Architectural Boundaries:**
* **Target Service:** (e.g., `Order-Management-Service`)
* **Core Patterns:** (e.g., Clean Architecture, MediatR, Repository Pattern)
* **Infrastructure:** (e.g., Azure SQL, Redis, Service Bus Topic: `order-events`)

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)
*To prevent AI drift, this task must be executed in the following order. Each phase requires a build/test pass before moving to the next.*

### Phase 1: [e.g., Data Contracts & Persistence]
* **Logic:** Define the DTOs, Entity configurations, and DB Context changes.
* **Agent Instruction:** "Implement only the POCOs and Interface definitions. Do not add business logic yet."
* **Checkpoint:** Code compiles; Repository interfaces are defined.

### Phase 2: [e.g., Core Domain Logic & Validation]
* **Logic:** Implement Service/Command Handler logic. Include validation rules and error handling (using the `Result` pattern).
* **Agent Instruction:** "Based on Phase 1 interfaces, implement the business logic. Focus on the edge cases defined in Part 4."
* **Checkpoint:** Unit tests pass for all business rules.

### Phase 3: [e.g., API Surface & Integration]
* **Logic:** Wire up the Controller/Minimal API endpoints. Add middleware, logging, and external service calls (e.g., Service Bus).
* **Agent Instruction:** "Expose the logic via the API. Ensure proper HTTP status codes (201 Created, 400 Bad Request, etc.)."
* **Checkpoint:** End-to-end flow works.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS
*Mandatory rules for the AI Agent to follow to ensure long-term maintainability.*

* **Coding Standards:** (e.g., C# 12 Primary Constructors, File-scoped namespaces)
* **Security:** (e.g., Sanitize inputs, no PII in logs, use Managed Identity for Azure)
* **Performance:** (e.g., Use `ValueTask` where applicable, `AsNoTracking()` for reads)
* **Prohibited:** (e.g., No `dynamic` types, no logic in Controllers, no manual `new HttpClient()`)

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE
**Expected Output:**
* *Describe the successful return type and payload structure.*

**Unit Test Scenarios:**
* [ ] **Happy Path:** Valid input returns 200/201.
* [ ] **Validation:** Missing required field returns 400.
* [ ] **Resilience:** Database timeout is handled with retry logic.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES
* **Impacted Components:** (List any shared libraries or downstream microservices affected)
* **Dependent Tasks:** (Link to any tasks that must be finished first)
* **Anti-Drift Log:** (Optional: Note if the AI suggested a logic change during implementation)