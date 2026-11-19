# Project Master Specification
## Autonomous AI Geopolitical and Military Simulation

### 1. Project Identity

A 2D geopolitical and military simulation designed primarily as a showcase and experimental environment for an autonomous artificial intelligence system.

The objective is an AI capable of independently interpreting an uncertain, continuously evolving geopolitical environment, coordinating specialized information-processing systems, making strategic and operational decisions, managing resources, and learning from the consequences of its actions through reinforcement learning.

This is not a conventional video game. The AI is the primary subject. The simulation exists to provide an environment in which its decision-making, adaptability, mistakes, learning and emergent behavior can be observed and evaluated.

The world must continue operating independently of the AI. The AI is not the protagonist, and not every event needs to involve its country or immediate responsibilities.

### 2. Visual Identity and Interface

2D top-down map with a slightly angular visual perspective. Cold War-inspired geography, with military units, installations, transportation routes and geographical features as custom 2D assets or drawn elements. Inspired by advanced cartographic and intelligence-visualization interfaces.

Palette: white, black, gray, red as the primary accent.

Visual characteristics: contour lines; borders and territorial boundaries; unit symbols and illustrated assets; animated movement and transport paths; sensor coverage and observation indicators; data-flow visualization; tactical markings and target annotations; telemetry and analytical information.

The interface should resemble a dedicated intelligence and operations workstation rather than a conventional website dashboard. Avoid ordinary top-navigation tabs as the primary interaction model. Prefer contextual panels, map interactions, floating controls, terminal interfaces and persistent operational information.

### 3. Two Independent Representations

#### Window A: Actual World Simulation

The complete ground-truth state, independent of what the AI knows or believes.

- Military operations: engagements, attackers and targets, successes and failures, impact locations, casualties and losses, territorial developments.
- Geopolitical developments: negotiations, disagreements, alliances, deteriorating relationships, changes in relationships.
- Logistics: ground, naval and aerial supply movements, successful and failed deliveries, intercepted or destroyed shipments, shortages.
- World activity: independent movements and operations of other countries, developments unrelated to the AI, events that may become relevant later.

The world evolves even when the AI has no involvement. This window is the authoritative ground truth used to evaluate the AI's decisions. The AI must not have unrestricted access to it or to the underlying complete state.

#### Window B: AI Operational Map

The AI's available information, internal understanding and operational decisions. It must not display the complete world state. The AI receives fragmented information through specialized information-processing systems and constructs its own operational picture.

It must be capable of: interpreting incoming information; estimating unit positions; identifying possible targets; tracking observed objects; assessing uncertainty; identifying strategically relevant developments; establishing areas requiring observation; placing operational points on its own map.

Active map interaction by the AI is essential. The AI must not receive a preconfigured collection of target markers. It decides where to place points based on observations, reports, instructions and assessments. Points may represent: expected enemy activity; suspected target locations; areas requiring surveillance; potential artillery engagement locations; important supply routes; areas requiring further intelligence; locations where tracking should be maintained.

The map must visually distinguish confirmed observations, estimated positions, suspected targets and AI-generated predictions.

### 4. Geopolitical World Simulation

Multiple independent countries with their own activities, interests, relationships and operations. The simulation must not revolve exclusively around the AI's assigned country.

Examples: USA-Italy negotiations deteriorating; Japan dispatching supplies to an installation; a shipment taking an unfavorable route; military clashes; supply interception; alliances forming or deteriorating; changes in capabilities and resources. These events can influence other events and future circumstances. Not every event requires AI intervention.

The AI must distinguish between: (1) information worth knowing, (2) information worth monitoring, (3) information that changes its strategic assessment, (4) information requiring an immediate decision.

### 5. Information and Sensor Systems

The AI receives information through specialized information-processing agents which provide filtered observations and assessments to a centralized intelligence system.

Sources: frontline communications; High Command instructions; radar; sonar; optical sensors; detection uncertainty; target identification; object tracking; reconnaissance; logistics reports; geopolitical intelligence.

Information must not automatically reveal ground truth. The AI operates with incomplete, delayed, uncertain and potentially conflicting observations.

### 6. Centralized Intelligence Architecture

One centralized intelligence supported by specialized agents. Agents process information within their domains; the centralized intelligence receives filtered outputs and coordinates decision-making: assessing the broader situation, prioritizing information, establishing priorities, evaluating competing objectives, allocating resources, coordinating artillery, managing logistics, assessing risks and consequences, making autonomous decisions.

It must consider relationships between systems instead of optimizing each subsystem independently. Specialized agents provide information and assessments rather than controlling strategic direction.

### 7. Autonomous Decision-Making Responsibilities

A. Intelligence and observation: interpret sensor information; identify important developments; establish observation points; track objects; update the operational map.

B. Artillery coordination: identify potential targets; evaluate relevance; coordinate artillery assets; assign resources; consider simulated projectile physics (trajectory, velocity, gravity, drag, impact, uncertainty). All physical weapon calculations are confined to the fictional simulation environment.

C. Strategic assessment: evaluate competing priorities; consider the geopolitical environment; recognize opportunities and threats; consider alliances; account for long-term consequences.

D. Logistics: identify supply requirements; coordinate ground, naval and aerial transport; evaluate transport conditions; account for losses and failures; prioritize resources.

E. Resource and installation management: manage the assigned installation, the Nest: ammunition replenishment, maintenance, equipment availability, resource allocation, environmental management including room temperature, operational readiness.

F. Consequence assessment: friendly units in the affected area; potential friendly casualties; allied capabilities; enemy capabilities; equipment and personnel availability; consequences of delay; broader strategic situation; alternatives. The AI must handle trade-offs rather than maximizing immediate destruction or short-term success.

### 8. Reinforcement Learning

Reinforcement learning through simulated outcomes is the primary learning approach, aiming for adaptive decision-making and meaningful emergent behavior. Learning through decisions, simulated outcomes, environmental feedback, reward signals, repeated interactions and human-provided corrections, accounting for long-term consequences.

Reward design must consider: operational effectiveness; resource sustainability; friendly safety; strategic consequences; decision quality under uncertainty; successful information interpretation. The algorithm and reward formulation are architectural decisions to be established during development.

### 9. Human-in-the-Loop Evaluation and Correction

A human operator evaluates performance and does not control every decision. The operator identifies failures and submits corrections through a terminal interface using the convention:

`operating [name] [number]`

Reportable failures: failure to achieve the target; incorrect target assessment; friendly fire; morally inappropriate decisions; tactical mistakes; incorrect supply allocation; misleading or unsuccessful transport decisions.

The terminal preserves the reported operation and its information. Feedback is converted into structured information for training and evaluation.

### 10. Model Updates and Parameter Tracking

Model updates are recorded independently of the simulation in a standalone file or structured collection tracking: model versions; parameter configurations; training iterations; training data references; human corrections; evaluation results; performance changes; experiment information.

Improvements must be measurable, versions comparable. Human corrections must not automatically overwrite parameters without a training and evaluation process.

### 11. Technology Stack

Frontend: HTML, CSS, JavaScript, custom 2D map rendering and assets.
Simulation backend: C#, .NET; authoritative world state, events and interactions, unit behavior, sensor simulation, ground-truth recording.
Machine learning: Python, PyTorch; reinforcement learning, specialized processing systems, evaluation and training.

C# and Python communicate through a clearly defined interface with structured data exchange. The frontend remains separate from the simulation and learning systems.

### 12. Core Architectural Principles

1. The simulation owns the actual world state.
2. The AI never receives unrestricted access to ground truth.
3. The AI constructs its own operational map from available information.
4. The AI can independently place and update observation and operational points.
5. Specialized agents process information for a centralized intelligence.
6. The centralized intelligence makes autonomous decisions.
7. The world continues evolving independently of the AI.
8. Events can be relevant without requiring immediate intervention.
9. Decisions are evaluated against actual simulated outcomes.
10. Reinforcement learning accounts for uncertainty and long-term consequences.
11. Human feedback is recorded and evaluated systematically.
12. Model versions and parameter changes are traceable.

### 13. Development Philosophy

Proceed incrementally. Establish the world simulation, information separation, communication architecture and decision-making interfaces before complex learning systems. Avoid machine learning where conventional algorithms are more appropriate. Do not add features merely because they are associated with military simulations. Every subsystem must serve a defined purpose.
