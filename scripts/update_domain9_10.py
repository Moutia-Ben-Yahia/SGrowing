import os
import re

def insert_sections(filepath, class_diagram, db_schema, seq_diagram):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Insert Class Diagram into Section 2
    if 'classDiagram' not in content:
        sec3_match = re.search(r'\n## 3\.\s+Technology Stack', content)
        if sec3_match:
            idx = sec3_match.start()
            content = content[:idx] + "\n" + class_diagram.strip() + "\n\n---\n" + content[idx:]

    # 2. Insert Detailed DB Schema into Section 6
    if 'Comprehensive Database Schema Specification' not in content:
        sec_next_match = re.search(r'\n## [789]\.\s+', content)
        if sec_next_match:
            idx = sec_next_match.start()
            content = content[:idx] + "\n" + db_schema.strip() + "\n\n---\n" + content[idx:]

    # 3. Insert Sequence Diagram into Section 7 / before Strategic Value
    if 'sequenceDiagram' not in content:
        sec_strat_match = re.search(r'\n## [789]\.\s+Strategic Value', content)
        if sec_strat_match:
            idx = sec_strat_match.start()
            content = content[:idx] + "\n" + seq_diagram.strip() + "\n\n---\n" + content[idx:]

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {filepath} successfully!")

# ==========================================
# DOMAIN 9: BOT FRAMEWORKS
# ==========================================

# 1. botpress
botpress_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class BotpressServer {
        +start() Promise~Void~
        +loadBot(botId) BotInstance
    }

    class DialogueEngine {
        +processEvent(event) Promise~Void~
        +transitionNode(flow, nodeName) Void
    }

    class NLUManager {
        +predictIntent(text) NLUResult
        +extractEntities(text) List~Entity~
    }

    class BotInstance {
        +String botId
        +JSON config
        +List~Flow~ flows
    }

    BotpressServer --> BotInstance : manages
    BotInstance --> DialogueEngine : runs_dialogue
    DialogueEngine --> NLUManager : uses_nlu
```
"""

botpress_db = """
### 6.1 Comprehensive Database Schema Specification

Botpress stores conversation states and bot definitions in PostgreSQL or SQLite.

#### Table: `bp_dialogue_sessions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(255)` | `PRIMARY KEY` | - | Session ID |
| `bot_id` | `VARCHAR(100)` | `NOT NULL` | - | Target bot ID |
| `channel` | `VARCHAR(50)` | `NOT NULL` | `'web'` | Medium |
| `state` | `JSON` | `NOT NULL` | `'{}'` | Dialog state variables |
"""

botpress_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Chat User
    participant Server as Botpress Server
    participant NLU as NLUManager
    participant Flow as DialogueEngine

    User->>Server: Inbound message ("I need a refund")
    Server->>NLU: predictIntent("I need a refund")
    NLU-->>Server: Intent: `billing.refund` (0.94)
    Server->>Flow: Transition to RefundFlow -> Node AskInvoice
    Flow-->>User: Bot response: "Please provide your invoice number"
```
"""

# 2. ChatbotX
chatbotx_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class ChatEngine {
        +generateReply(sessionId, prompt) String
    }

    class DocumentIndexer {
        +embedText(docText) List~Float~
    }

    ChatEngine --> DocumentIndexer : retrieves_context
```
"""

chatbotx_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `chatbots`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Chatbot ID |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Bot display name |
| `model` | `VARCHAR(100)` | `NOT NULL` | `'gpt-4o'` | LLM model |
"""

chatbotx_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Bot as ChatEngine
    participant RAG as RetrievalEngine
    participant LLM as OpenAI

    User->>Bot: Asks product question
    Bot->>RAG: Find relevant document chunks
    RAG-->>Bot: Matched chunks
    Bot->>LLM: Generate response with context
    LLM-->>Bot: Stream tokens
    Bot-->>User: Display answer
```
"""

# 3. aiogram
aiogram_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Bot {
        +String token
        +sendMessage(chatId, text) Message
    }

    class Dispatcher {
        +feedUpdate(bot, update) Void
        +includeRouter(router) Void
    }

    class Router {
        +message: MessageObserver
        +callback_query: CallbackQueryObserver
    }

    class FSMContext {
        +setState(state) Void
        +getData() Dict
    }

    Dispatcher --> Bot : uses
    Dispatcher "1" *-- "many" Router : includes
    Router ..> FSMContext : passes_to_handler
```
"""

aiogram_db = """
### 6.1 Comprehensive Database Schema Specification

Aiogram utilizes Redis or Memory storage for Finite State Machine (FSM) contexts.

#### Table: `fsm_states`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `key` | `TEXT` | `PRIMARY KEY` | - | Bot+User+Chat state key |
| `state` | `TEXT` | `NULLABLE` | `NULL` | Active FSM state |
| `data` | `TEXT` | `NOT NULL` | `'{}'` | Serialized state JSON |
"""

aiogram_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Telegram User
    participant TG as Telegram API
    participant DP as aiogram Dispatcher
    participant Router as Command Router
    participant FSM as FSM Storage

    User->>TG: Send "/start"
    TG->>DP: Webhook Update JSON
    DP->>Router: Resolve matching handler
    Router->>FSM: Set state = OrderFlow:WaitingForPhone
    Router->>TG: bot.send_message(chat_id, "Please share phone:")
    TG-->>User: Render prompt
```
"""

# 4. grammY
grammy_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class GrammyBot {
        +use(middleware) Void
        +command(name, handler) Void
        +start() Void
    }

    class Context {
        +Message message
        +reply(text) Promise~Message~
    }

    GrammyBot --> Context : provides
```
"""

grammy_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `sessions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `key` | `VARCHAR(255)` | `PRIMARY KEY` | - | Session key |
| `value` | `TEXT` | `NOT NULL` | - | Session JSON |
"""

grammy_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Telegram User
    participant Bot as grammY Bot
    participant Middleware as Middleware Chain

    User->>Bot: Message
    Bot->>Middleware: Execute sequential middleware
    Middleware-->>User: ctx.reply("Received!")
```
"""

# 5. discord.js
discordjs_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Client {
        +login(token) Promise~String~
        +on(event, listener) Void
    }

    class CommandInteraction {
        +reply(content) Promise~Void~
    }

    Client --> CommandInteraction : emits
```
"""

discordjs_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `guild_settings`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `guild_id` | `VARCHAR(64)` | `PRIMARY KEY` | - | Discord Guild Snowflake |
| `prefix` | `VARCHAR(10)` | `NOT NULL` | `'!'` | Command prefix |
"""

discordjs_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Discord Member
    participant Gateway as Discord Gateway (WSS)
    participant Client as discord.js Client

    User->>Gateway: Slash command `/ping`
    Gateway->>Client: Event: `interactionCreate`
    Client-->>User: interaction.reply("Pong! 42ms")
```
"""

# 6. Red-DiscordBot
redbot_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Red {
        +load_extension(cog) Void
    }

    class Cog {
        +name: String
    }

    Red "1" *-- "many" Cog : loads
```
"""

redbot_db = """
### 6.1 Comprehensive Database Schema Specification

Red-DiscordBot manages JSON / MongoDB / PostgreSQL data layers.

#### Table: `red_configs`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `cog_name` | `VARCHAR(100)` | `PRIMARY KEY` | - | Cog name |
| `settings` | `JSON` | `NOT NULL` | `'{}'` | Stored attributes |
"""

redbot_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Red as Red Bot Core
    participant Cog as Audio Cog

    User->>Red: `[p]play lofi hip hop`
    Red->>Cog: Delegate to Audio Cog
    Cog-->>User: "Now playing: Lofi Beats"
```
"""

# 7. TomoriBot
tomoribot_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class TomoriBotClient {
        +start() Void
    }

    class MusicPlayer {
        +playTrack(voiceChannel, url) Void
    }

    TomoriBotClient --> MusicPlayer : controls
```
"""

tomoribot_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `guild_configs`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `guild_id` | `VARCHAR(64)` | `PRIMARY KEY` | - | Guild Snowflake |
| `volume` | `INTEGER` | `NOT NULL` | `100` | Playback volume |
"""

tomoribot_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Member
    participant Bot as TomoriBot
    participant Voice as Discord Voice Channel

    User->>Bot: `!play song.mp3`
    Bot->>Voice: Join voice channel & stream audio
```
"""

# ==========================================
# DOMAIN 10: FORMS, SURVEYS & FEEDBACK
# ==========================================

# 8. formbricks
formbricks_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SurveyService {
        +createSurvey(data) Survey
        +getSurveysByEnvironment(envId) List~Survey~
    }

    class ResponseService {
        +submitResponse(surveyId, data) Response
    }

    class Survey {
        +String id
        +String name
        +String type
        +JSON questions
    }

    class Response {
        +String id
        +String surveyId
        +JSON data
    }

    SurveyService --> Survey : manages
    ResponseService --> Response : creates
    Survey "1" *-- "many" Response : collects
```
"""

formbricks_db = """
### 6.1 Comprehensive Database Schema Specification

Formbricks runs on PostgreSQL with Prisma ORM.

#### Table: `Survey`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Survey ID |
| `name` | `TEXT` | `NOT NULL` | - | Survey name |
| `type` | `TEXT` | `NOT NULL` | `'web'` | Type (`web`, `email`, `link`) |
| `status` | `TEXT` | `NOT NULL` | `'draft'` | Status (`draft`, `inProgress`, `completed`) |
| `questions` | `JSONB` | `NOT NULL` | `'[]'` | Structured question blocks |

*Indexes:* `Survey_status_idx` (`(status)`).

---

#### Table: `Response`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `TEXT` | `PRIMARY KEY` | `cuid()` | Response ID |
| `surveyId` | `TEXT` | `NOT NULL, FK -> Survey(id)` | - | Associated survey |
| `data` | `JSONB` | `NOT NULL` | `'{}'` | User answers map |
| `createdAt` | `TIMESTAMP(3)` | `NOT NULL` | `CURRENT_TIMESTAMP` | Submission time |

*Indexes:* `Response_surveyId_idx` (`(surveyId)`).
"""

formbricks_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as App User
    participant SDK as Formbricks Client SDK
    participant API as Formbricks API
    participant DB as PostgreSQL Database

    User->>SDK: User visits `/pricing` page
    SDK->>SDK: Evaluate display trigger: Target page matches
    SDK-->>User: Render in-app modal survey ("How did you hear about us?")
    User->>SDK: Selects "Twitter / X"
    SDK->>API: POST /api/v1/client/responses (surveyId, answers)
    API->>DB: INSERT into Response (surveyId, data)
    API-->>SDK: 201 Response Recorded
    SDK-->>User: Display thank you toast
```
"""

# 9. heyform
heyform_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class FormService {
        +createForm(title, fields) Form
    }

    class SubmissionService {
        +recordSubmission(formId, answers) Submission
    }

    FormService --> SubmissionService : connects
```
"""

heyform_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `submissions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `VARCHAR(24)` | `PRIMARY KEY` | - | Submission ID |
| `form_id` | `VARCHAR(24)` | `NOT NULL` | - | Form ID |
| `answers` | `JSON` | `NOT NULL` | `'{}'` | Answers |
"""

heyform_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Respondent as Respondent
    participant Form as HeyForm Web
    participant DB as Database

    Respondent->>Form: Submit completed form
    Form->>DB: Save submission answers
    Form-->>Respondent: Completion screen
```
"""

# 10. OpnForm
opnform_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class Form {
        +BigInt id
        +String title
        +List~Field~ fields
    }

    class Submission {
        +BigInt id
        +BigInt formId
        +JSON content
    }

    Form "1" *-- "many" Submission : collects
```
"""

opnform_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `submissions`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `BIGINT` | `PRIMARY KEY AUTO_INCREMENT` | - | ID |
| `form_id` | `BIGINT` | `NOT NULL` | - | Parent form |
| `content` | `JSON` | `NOT NULL` | - | Submitted values |
"""

opnform_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant App as OpnForm
    participant DB as MySQL DB

    User->>App: Submit form
    App->>DB: INSERT into submissions
    App-->>User: Thank you
```
"""

# 11. formizee
formizee_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class EndpointService {
        +processSubmission(endpointId, payload) Result
    }
```
"""

formizee_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `endpoints`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Endpoint token |
| `name` | `VARCHAR(255)` | `NOT NULL` | - | Endpoint title |
"""

formizee_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Form as Static Form
    participant Edge as Cloudflare Worker Endpoint
    participant DB as D1 / PostgreSQL

    Form->>Edge: POST /f/:endpointId (formData)
    Edge->>Edge: Spam filtering & captcha verification
    Edge->>DB: Persist submission
    Edge-->>Form: 302 Redirect to Success URL
```
"""

# 12. LimeSurvey
limesurvey_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SurveyAdminController {
        +activateSurvey(surveyId) Void
    }

    class ResponseModel {
        +saveResponse(surveyId, answers) Int
    }

    SurveyAdminController --> ResponseModel : coordinates
```
"""

limesurvey_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `lime_surveys`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `sid` | `INT` | `PRIMARY KEY` | - | Survey numeric ID |
| `active` | `VARCHAR(1)` | `NOT NULL` | `'N'` | Active flag |
"""

limesurvey_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Participant as Participant
    participant Survey as LimeSurvey Engine
    participant DB as MySQL Database

    Participant->>Survey: Fill token & answers
    Survey->>DB: INSERT into lime_survey_1234
    Survey-->>Participant: Survey submitted
```
"""

# 13. survey-library
surveylib_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class SurveyModel {
        +JSON jsonDefinition
        +onComplete: Event
        +render() Void
    }
```
"""

surveylib_db = """
### 6.1 Comprehensive Database Schema Specification

Client-side JSON schema model.
"""

surveylib_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Model as SurveyModel

    User->>Model: Selects answer options
    Model->>Model: Evaluates conditional logic
    Model-->>User: Renders next page
```
"""

# 14. fider
fider_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class IdeaService {
        +submitIdea(title, desc) Idea
        +vote(ideaId, userId) Void
    }

    class Idea {
        +Int id
        +String title
        +Int votesCount
        +String status
    }

    IdeaService --> Idea : manages
```
"""

fider_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `ideas`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `SERIAL` | `PRIMARY KEY` | - | Idea ID |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Feature request title |
| `votes_count` | `INTEGER` | `NOT NULL` | `1` | Upvotes count |
| `status` | `VARCHAR(50)` | `NOT NULL` | `'open'` | Status (`open`, `planned`, `completed`) |
"""

fider_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as Customer
    participant Fider as Fider App
    participant DB as PostgreSQL Database

    User->>Fider: Submit Feature Idea & Upvote
    Fider->>DB: INSERT into ideas & votes
    Fider-->>User: Idea published to roadmap board
```
"""

# 15. logchimp
logchimp_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class RoadmapService {
        +createPost(data) Post
    }
```
"""

logchimp_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `posts`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | Post ID |
| `title` | `VARCHAR(255)` | `NOT NULL` | - | Feedback title |
"""

logchimp_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant App as Logchimp
    participant DB as DB

    User->>App: Upvotes Roadmap Feature
    App->>DB: Increment votes
    App-->>User: Updated vote count
```
"""

# 16. Logiq
logiq_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class FeedbackCollector {
        +ingestFeedback(text) Void
    }
```
"""

logiq_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `feedback_items`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | ID |
| `text` | `TEXT` | `NOT NULL` | - | Feedback text |
"""

logiq_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Client as App
    participant Logiq as Logiq Collector
    participant DB as Storage

    Client->>Logiq: Post user sentiment
    Logiq->>DB: Store item
```
"""

# 17. dsforms
dsforms_cd = """
### 2.1 Core Domain & Service Class Architecture

```mermaid
classDiagram
    class FormGenerator {
        +buildForm(schema) DynamicForm
    }
```
"""

dsforms_db = """
### 6.1 Comprehensive Database Schema Specification

#### Table: `ds_forms`
| Column | Type | Constraints | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `id` | `UUID` | `PRIMARY KEY` | `gen_random_uuid()` | ID |
| `schema` | `JSON` | `NOT NULL` | - | Form spec |
"""

dsforms_seq = """
### 7.5 End-to-End Operational Lifecycle Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant Form as DSForms
    participant Backend as Backend

    User->>Form: Submit fields
    Form->>Backend: Post data
    Backend-->>User: Verified
```
"""

# Execute insertions
insert_sections('docs/wiki/botpress.md', botpress_cd, botpress_db, botpress_seq)
insert_sections('docs/wiki/ChatbotX.md', chatbotx_cd, chatbotx_db, chatbotx_seq)
insert_sections('docs/wiki/aiogram.md', aiogram_cd, aiogram_db, aiogram_seq)
insert_sections('docs/wiki/grammY.md', grammy_cd, grammy_db, grammy_seq)
insert_sections('docs/wiki/discord.js.md', discordjs_cd, discordjs_db, discordjs_seq)
insert_sections('docs/wiki/Red-DiscordBot.md', redbot_cd, redbot_db, redbot_seq)
insert_sections('docs/wiki/TomoriBot.md', tomoribot_cd, tomoribot_db, tomoribot_seq)

insert_sections('docs/wiki/formbricks.md', formbricks_cd, formbricks_db, formbricks_seq)
insert_sections('docs/wiki/heyform.md', heyform_cd, heyform_db, heyform_seq)
insert_sections('docs/wiki/OpnForm.md', opnform_cd, opnform_db, opnform_seq)
insert_sections('docs/wiki/formizee.md', formizee_cd, formizee_db, formizee_seq)
insert_sections('docs/wiki/LimeSurvey.md', limesurvey_cd, limesurvey_db, limesurvey_seq)
insert_sections('docs/wiki/survey-library.md', surveylib_cd, surveylib_db, surveylib_seq)
insert_sections('docs/wiki/fider.md', fider_cd, fider_db, fider_seq)
insert_sections('docs/wiki/logchimp.md', logchimp_cd, logchimp_db, logchimp_seq)
insert_sections('docs/wiki/Logiq.md', logiq_cd, logiq_db, logiq_seq)
insert_sections('docs/wiki/dsforms.md', dsforms_cd, dsforms_db, dsforms_seq)
