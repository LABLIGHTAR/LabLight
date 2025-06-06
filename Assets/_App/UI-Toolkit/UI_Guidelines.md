# LabLight UI Toolkit Architecture Guidelines

This document outlines the architecture, naming conventions, and best practices for creating UI in this project using UI Toolkit. Adhering to these guidelines will ensure a consistent, maintainable, and scalable UI codebase.

---

## 1. Core Architecture: Windows and Components

The UI is built on a "Window" and "Component" architecture.

- **Windows**: A Window is a top-level visual container, typically occupying a significant portion of the screen (e.g., the entire user login sequence, the protocol browser, the main dashboard). Each Window has a dedicated controller script and acts as a host for smaller, swappable UI Components.
- **Components**: A Component is a modular, self-contained piece of UI that represents a specific part of a view (e.g., a user list, a login form, a password field with validation). Components are swapped in and out of a Window's content area by the Window's controller.

---

## 2. Naming Conventions

A strict naming convention is used to easily identify the role of each file.

### UXML Files (`.uxml`)

- **Window UXML**: File names must end with `Window`.
  - _Example_: `UserLoginWindow.uxml`, `DashboardWindow.uxml`
- **Component UXML**: File names must end with `Component`.
  - _Example_: `UserSelectionComponent.uxml`, `PasswordInputComponent.uxml`

### C# Scripts (`.cs`)

- **Window Controllers**: Script names must match their corresponding Window UXML file and end with `Controller`.
  - _Example_: `UserLoginWindowController.cs`
- **Component Scripts**: Script names must match their corresponding Component UXML file.
  - _Example_: `UserSelectionComponent.cs`

---

## 3. Scripting Guidelines

The inheritance of a script depends on its role as either a Window Controller or a Component.

### BaseWindowController

To streamline window creation, all Window Controllers should inherit from `BaseWindowController`. This base class provides essential, out-of-the-box functionality:

- **Inspector-Based Sizing**: Set the window's dimensions directly in the Unity Inspector without writing extra code.
- **Component Swapping**: A standardized `SwapComponent(container, component)` method for replacing content in any `VisualElement` container.
- **Show/Hide Control**: Simple `Show()` and `Hide()` methods to manage the window's visibility.

### Window Controllers (`...WindowController.cs`):

- **Should** inherit from `BaseWindowController`.
- These are attached to a `GameObject` in the scene that also contains a `UIDocument` component.
- They are responsible for managing the lifecycle of UI Components, handling application-level logic, and communicating with other services by leveraging the features of the base class.

- **Component Scripts** (`...Component.cs`):
  - **Must** inherit from `UnityEngine.UIElements.VisualElement`.
  - These are **not** attached to GameObjects. They are instantiated in code by a Window Controller and added to the visual tree.
  - They are responsible for their own internal logic and expose C# events (e.g., `OnSubmit`, `OnCancel`) to communicate user actions back to the hosting Window Controller.
