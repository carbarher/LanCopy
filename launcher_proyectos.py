from __future__ import annotations

import json
import os
import shutil
import subprocess
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk


ROOT = Path(__file__).resolve().parent
CONFIG_PATH = ROOT / "launcher_proyectos.json"
CREATE_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)


class ProjectLauncher(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("Lanzador de proyectos")
        self.geometry("760x500")
        self.minsize(680, 420)
        self.configure(bg="#12161c")

        self.projects = self._load_projects()
        self.visible_rows: list[int | None] = []

        self.search_var = tk.StringVar(value="")
        self.category_var = tk.StringVar(value="Todas")
        self.status_var = tk.StringVar(value="Listo")

        self._apply_dark_theme()
        self._build_ui()
        self._refresh_categories()
        self._refresh_list()

    def _apply_dark_theme(self) -> None:
        style = ttk.Style(self)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass

        bg = "#12161c"
        panel = "#1a212b"
        border = "#2b3542"
        fg = "#e6edf3"
        subtle = "#93a4b7"
        accent = "#3a86ff"

        style.configure(".", background=bg, foreground=fg)
        style.configure("TFrame", background=bg)
        style.configure("TLabel", background=bg, foreground=fg)
        style.configure(
            "TButton",
            background=panel,
            foreground=fg,
            bordercolor=border,
            padding=(10, 6),
        )
        style.map("TButton", background=[("active", "#243142")])
        style.configure(
            "TEntry",
            fieldbackground=panel,
            foreground=fg,
            insertcolor=fg,
            bordercolor=border,
        )
        style.configure(
            "Dark.TCombobox",
            fieldbackground=panel,
            background=panel,
            foreground=fg,
            bordercolor=border,
            arrowcolor=fg,
        )
        style.map(
            "Dark.TCombobox",
            fieldbackground=[("readonly", panel)],
            foreground=[("readonly", fg)],
            selectforeground=[("readonly", fg)],
            selectbackground=[("readonly", panel)],
            background=[("readonly", panel)],
        )
        style.configure("Muted.TLabel", background=bg, foreground=subtle)
        style.configure("Accent.TLabel", background=bg, foreground=accent)

    def _load_projects(self) -> list[dict[str, str]]:
        if not CONFIG_PATH.exists():
            return []
        try:
            data = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        except Exception as exc:
            messagebox.showerror(
                "Error de configuracion",
                f"No se pudo leer {CONFIG_PATH.name}: {exc}",
            )
            return []

        items = data.get("projects", [])
        projects: list[dict[str, str | bool]] = []
        for item in items:
            if not isinstance(item, dict):
                continue
            name = str(item.get("name", "")).strip()
            path = str(item.get("path", "")).strip()
            cwd = str(item.get("cwd", "")).strip()
            category = str(item.get("category", "General")).strip() or "General"
            icon = str(item.get("icon", "APP")).strip() or "APP"
            description = str(item.get("description", "")).strip()
            favorite = bool(item.get("favorite", False))
            if not name or not path:
                continue
            projects.append(
                {
                    "name": name,
                    "path": path,
                    "cwd": cwd,
                    "category": category,
                    "icon": icon,
                    "description": description,
                    "favorite": favorite,
                }
            )
        return projects

    def _build_ui(self) -> None:
        container = ttk.Frame(self, padding=14)
        container.pack(fill=tk.BOTH, expand=True)

        header = ttk.Label(
            container,
            text="Proyectos",
            font=("Segoe UI", 16, "bold"),
        )
        header.pack(anchor="w")

        info = ttk.Label(
            container,
            text=(
                "Doble clic para lanzar. "
                "Favoritos arriba. Filtra por categoria o buscador."
            ),
            style="Muted.TLabel",
        )
        info.pack(anchor="w", pady=(4, 10))

        search_row = ttk.Frame(container)
        search_row.pack(fill=tk.X, pady=(0, 8))

        ttk.Label(search_row, text="Buscar:").pack(side=tk.LEFT)
        entry = ttk.Entry(search_row, textvariable=self.search_var)
        entry.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(8, 0))
        entry.bind("<KeyRelease>", lambda _e: self._refresh_list())

        ttk.Label(search_row, text="Categoria:").pack(side=tk.LEFT, padx=(12, 0))
        self.category_combo = ttk.Combobox(
            search_row,
            textvariable=self.category_var,
            state="readonly",
            width=18,
            style="Dark.TCombobox",
        )
        self.category_combo.pack(side=tk.LEFT, padx=(8, 0))
        self.category_combo.bind("<<ComboboxSelected>>", lambda _e: self._refresh_list())

        list_frame = ttk.Frame(container)
        list_frame.pack(fill=tk.BOTH, expand=True)

        self.listbox = tk.Listbox(
            list_frame,
            activestyle="none",
            font=("Consolas", 11),
            selectmode=tk.SINGLE,
            bg="#0f141b",
            fg="#dbe5f0",
            selectbackground="#27446e",
            selectforeground="#ffffff",
            highlightthickness=1,
            highlightbackground="#2b3542",
            relief=tk.FLAT,
        )
        self.listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.listbox.bind("<Double-Button-1>", lambda _e: self._launch_selected())
        self.listbox.bind("<<ListboxSelect>>", lambda _e: self._on_selection_changed())

        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.listbox.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.listbox.config(yscrollcommand=scrollbar.set)

        actions = ttk.Frame(container)
        actions.pack(fill=tk.X, pady=(10, 0))

        ttk.Button(actions, text="Lanzar", command=self._launch_selected).pack(side=tk.LEFT)
        ttk.Button(actions, text="Recargar", command=self._reload_config).pack(
            side=tk.LEFT,
            padx=(8, 0),
        )
        ttk.Button(actions, text="Abrir config", command=self._open_config).pack(
            side=tk.LEFT,
            padx=(8, 0),
        )

        status = ttk.Label(container, textvariable=self.status_var, style="Accent.TLabel")
        status.pack(anchor="w", pady=(8, 0))

    def _refresh_categories(self) -> None:
        categories = sorted(
            {
                str(item.get("category", "General"))
                for item in self.projects
                if str(item.get("category", "")).strip()
            }
        )
        values = ["Todas"] + categories
        self.category_combo["values"] = values
        if self.category_var.get() not in values:
            self.category_var.set("Todas")

    def _project_matches(self, item: dict[str, str | bool], query: str) -> bool:
        if not query:
            return True
        haystack = " ".join(
            [
                str(item.get("name", "")),
                str(item.get("path", "")),
                str(item.get("category", "")),
                str(item.get("icon", "")),
                str(item.get("description", "")),
            ]
        ).lower()
        return query in haystack

    def _format_display_name(self, item: dict[str, str | bool]) -> str:
        icon = str(item.get("icon", "APP")).strip() or "APP"
        name = str(item.get("name", ""))
        category = str(item.get("category", "General"))
        description = str(item.get("description", "")).strip()
        if description:
            return f"[{icon}] {name}  <{category}>  -  {description}"
        return f"[{icon}] {name}  <{category}>"

    def _refresh_list(self) -> None:
        query = self.search_var.get().strip().lower()
        selected_category = self.category_var.get().strip()
        self.listbox.delete(0, tk.END)
        self.visible_rows.clear()

        favorites: list[int] = []
        others: list[int] = []

        for idx, item in enumerate(self.projects):
            category = str(item.get("category", "General"))
            if selected_category and selected_category != "Todas" and category != selected_category:
                continue
            if not self._project_matches(item, query):
                continue
            if bool(item.get("favorite", False)):
                favorites.append(idx)
            else:
                others.append(idx)

        inserted = 0
        if favorites:
            self.listbox.insert(tk.END, "=== FAVORITOS ===")
            self.visible_rows.append(None)
            inserted += 1
            for idx in favorites:
                self.listbox.insert(tk.END, self._format_display_name(self.projects[idx]))
                self.visible_rows.append(idx)
                inserted += 1

        if others:
            self.listbox.insert(tk.END, "=== PROYECTOS ===")
            self.visible_rows.append(None)
            inserted += 1
            for idx in others:
                self.listbox.insert(tk.END, self._format_display_name(self.projects[idx]))
                self.visible_rows.append(idx)
                inserted += 1

        # Marca visual de cabeceras.
        for row, idx in enumerate(self.visible_rows):
            if idx is None:
                self.listbox.itemconfig(row, fg="#7ea0c8")

        # Selecciona el primer elemento lanzable.
        for row, idx in enumerate(self.visible_rows):
            if idx is not None:
                self.listbox.selection_set(row)
                break

        launchable_count = len(favorites) + len(others)
        self.status_var.set(f"Mostrando {launchable_count} proyecto(s)")

    def _selected_project(self) -> dict[str, str | bool] | None:
        selected = self.listbox.curselection()
        if not selected:
            return None
        row = selected[0]
        if row >= len(self.visible_rows):
            return None
        project_idx = self.visible_rows[row]
        if project_idx is None:
            return None
        return self.projects[project_idx]

    def _launch_selected(self) -> None:
        project = self._selected_project()
        if project is None:
            self.status_var.set("Selecciona un proyecto para lanzar")
            return

        path = Path(str(project["path"])).expanduser()
        cwd_raw = str(project.get("cwd", "")).strip()
        cwd = Path(cwd_raw).expanduser() if cwd_raw else path.parent

        if not path.exists():
            messagebox.showerror(
                "Ruta no encontrada",
                f"No existe:\n{path}",
            )
            return

        try:
            self._launch_path(path, cwd)
            self.status_var.set(f"Lanzado: {project['name']}")
        except Exception as exc:
            messagebox.showerror(
                "Error al lanzar",
                f"No se pudo iniciar {project['name']}: {exc}",
            )

    def _launch_path(self, path: Path, cwd: Path) -> None:
        ext = path.suffix.lower()

        if ext in {".bat", ".cmd"}:
            subprocess.Popen(
                ["cmd.exe", "/c", str(path)],
                cwd=str(cwd),
                creationflags=subprocess.CREATE_NEW_CONSOLE,
            )
            return

        if ext == ".ps1":
            subprocess.Popen(
                [
                    "powershell.exe",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(path),
                ],
                cwd=str(cwd),
                creationflags=subprocess.CREATE_NEW_CONSOLE,
            )
            return

        if ext == ".py":
            python_gui = shutil.which("pythonw")
            if python_gui:
                subprocess.Popen(
                    [python_gui, str(path)],
                    cwd=str(cwd),
                    creationflags=CREATE_NO_WINDOW,
                )
                return

            pyw = shutil.which("pyw")
            if pyw:
                subprocess.Popen(
                    [pyw, "-3", str(path)],
                    cwd=str(cwd),
                    creationflags=CREATE_NO_WINDOW,
                )
                return

            subprocess.Popen(
                ["python", str(path)],
                cwd=str(cwd),
                creationflags=CREATE_NO_WINDOW,
            )
            return

        if ext == ".exe":
            subprocess.Popen(
                [str(path)],
                cwd=str(cwd),
                creationflags=CREATE_NO_WINDOW,
            )
            return

        os.startfile(str(path))

    def _on_selection_changed(self) -> None:
        project = self._selected_project()
        if project is None:
            return
        description = str(project.get("description", "")).strip()
        if description:
            self.status_var.set(f"{project['name']}: {description}")
        else:
            self.status_var.set(f"Seleccionado: {project['name']}")

    def _reload_config(self) -> None:
        self.projects = self._load_projects()
        self._refresh_categories()
        self._refresh_list()

    def _open_config(self) -> None:
        try:
            os.startfile(str(CONFIG_PATH))
        except Exception as exc:
            messagebox.showerror("Error", f"No se pudo abrir config: {exc}")


def main() -> None:
    app = ProjectLauncher()
    app.mainloop()


if __name__ == "__main__":
    main()
