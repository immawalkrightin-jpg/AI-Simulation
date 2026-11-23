import { parseOperation, resolveCategory, splitReport } from "./parser.js";

const HELP = [
  "operating <name> <number>",
  "  start a report",
  "operating <name> <number> <category> <note>",
  "  file a report in one line",
  "categories",
  "  list failure categories",
  "recent",
  "  show the latest reports",
  "cancel",
  "  abandon the current report",
  "clear",
  "  clear the screen",
  "close",
  "  hide the terminal"
];

const MAX_LINES = 300;
const MAX_HISTORY = 50;

export class Terminal {
  constructor(document, link) {
    this.document = document;
    this.link = link;
    this.root = document.getElementById("terminal");
    this.log = document.getElementById("terminal-log");
    this.form = document.getElementById("terminal-form");
    this.prompt = document.getElementById("terminal-prompt");
    this.input = document.getElementById("terminal-input");
    this.categories = [];
    this.draft = null;
    this.step = "idle";
    this.busy = false;
    this.history = [];
    this.position = 0;

    this.form.addEventListener("submit", (event) => {
      event.preventDefault();
      this.submitLine();
    });
    this.input.addEventListener("keydown", (event) => this.onKey(event));
    this.print("Operator terminal. Type help for commands.");
    this.updatePrompt();
  }

  toggle(open) {
    const show = open ?? this.root.hidden;
    this.root.hidden = !show;
    if (show) {
      this.input.focus();
    }
  }

  print(text, kind = "out") {
    const line = this.document.createElement("div");
    line.className = kind;
    line.textContent = text;
    this.log.append(line);
    while (this.log.childElementCount > MAX_LINES) {
      this.log.firstElementChild.remove();
    }
    this.log.scrollTop = this.log.scrollHeight;
  }

  updatePrompt() {
    if (this.step === "category") {
      this.prompt.textContent = `[${this.draft.operation} ${this.draft.number}] category>`;
    } else if (this.step === "description") {
      this.prompt.textContent = `[${this.draft.operation} ${this.draft.number} ${this.draft.category}] note>`;
    } else {
      this.prompt.textContent = ">";
    }
  }

  onKey(event) {
    event.stopPropagation();
    if (event.key === "Escape") {
      event.preventDefault();
      if (this.step === "idle") {
        this.toggle(false);
      } else {
        this.cancel();
        this.updatePrompt();
      }
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      this.recall(-1);
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      this.recall(1);
    }
  }

  recall(delta) {
    if (this.history.length === 0) {
      return;
    }
    this.position = Math.min(Math.max(this.position + delta, 0), this.history.length);
    this.input.value = this.history[this.position] ?? "";
  }

  remember(line) {
    const text = line.trim();
    if (text && this.history[this.history.length - 1] !== text) {
      this.history.push(text);
      if (this.history.length > MAX_HISTORY) {
        this.history.shift();
      }
    }
    this.position = this.history.length;
  }

  submitLine() {
    const line = this.input.value;
    if (this.busy) {
      return;
    }
    this.input.value = "";
    this.remember(line);
    this.print(`${this.prompt.textContent} ${line}`, "echo");
    this.busy = true;
    this.handle(line)
      .catch((error) => this.print(error.message, "error"))
      .finally(() => {
        this.busy = false;
        this.updatePrompt();
        this.input.focus();
      });
  }

  async handle(line) {
    const text = line.trim();
    if (!text) {
      return;
    }
    if (this.step === "category") {
      return this.onCategory(text);
    }
    if (this.step === "description") {
      return this.onDescription(text);
    }
    return this.onCommand(text);
  }

  async onCommand(text) {
    const word = text.toLowerCase();
    if (word === "help") {
      HELP.forEach((line) => this.print(line));
      return;
    }
    if (word === "clear") {
      this.log.replaceChildren();
      return;
    }
    if (word === "close") {
      this.toggle(false);
      return;
    }
    if (word === "cancel") {
      this.print("Nothing to cancel.");
      return;
    }
    if (word === "categories") {
      if (await this.loadCategories()) {
        this.listCategories();
      }
      return;
    }
    if (word === "recent") {
      await this.showRecent();
      return;
    }
    const parsed = parseOperation(text);
    if (parsed) {
      await this.begin(parsed);
      return;
    }
    this.print("Unrecognised command. Type help.", "error");
  }

  async begin(parsed) {
    if (!(await this.loadCategories())) {
      return;
    }
    this.draft = { operation: parsed.name, number: parsed.number, category: null, description: "" };
    if (!parsed.rest) {
      this.listCategories();
      this.step = "category";
      return;
    }
    const split = splitReport(parsed.rest, this.categories);
    if (!split.category) {
      this.print(`Unknown category "${split.token}".`, "error");
      this.listCategories();
      this.step = "category";
      return;
    }
    this.draft.category = split.category;
    if (!split.description) {
      this.step = "description";
      this.print("Describe what went wrong, then press Enter.");
      return;
    }
    this.draft.description = split.description;
    await this.submit();
  }

  onCategory(text) {
    if (text.toLowerCase() === "cancel") {
      this.cancel();
      return;
    }
    const category = resolveCategory(text, this.categories);
    if (!category) {
      this.print("Pick a category by name, prefix or number.", "error");
      this.listCategories();
      return;
    }
    this.draft.category = category;
    this.step = "description";
    this.print("Describe what went wrong, then press Enter.");
  }

  async onDescription(text) {
    if (text.toLowerCase() === "cancel") {
      this.cancel();
      return;
    }
    this.draft.description = text;
    await this.submit();
  }

  async submit() {
    const { operation, number, category, description } = this.draft;
    try {
      const saved = await this.link.submit({ operation, number, category, description });
      this.print(
        `Recorded ${saved.id.slice(0, 8)}: operating ${saved.operation} ${saved.number}, ${saved.category}, seed ${saved.seed}, tick ${saved.tick}.`,
        "ok"
      );
      this.reset();
    } catch (error) {
      this.step = "description";
      this.print(`Not recorded: ${error.message}. Enter the note again or type cancel.`, "error");
    }
  }

  cancel() {
    this.reset();
    this.print("Report cancelled.");
  }

  reset() {
    this.draft = null;
    this.step = "idle";
  }

  async loadCategories() {
    if (this.categories.length > 0) {
      return true;
    }
    try {
      this.categories = await this.link.categories();
      return this.categories.length > 0;
    } catch (error) {
      this.print(`Cannot reach the simulation API: ${error.message}`, "error");
      return false;
    }
  }

  listCategories() {
    this.categories.forEach((category, index) => this.print(`  ${index + 1}. ${category}`));
  }

  async showRecent() {
    try {
      const reports = await this.link.recent(10);
      if (reports.length === 0) {
        this.print("No reports recorded.");
        return;
      }
      reports.forEach((report) =>
        this.print(`${report.id.slice(0, 8)}  ${report.operation} ${report.number}  ${report.category}  seed ${report.seed} tick ${report.tick}`)
      );
    } catch (error) {
      this.print(`Cannot read reports: ${error.message}`, "error");
    }
  }
}
