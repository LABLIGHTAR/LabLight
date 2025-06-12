# LabLight UI Toolkit Style Guide

This document provides an overview of the core styling conventions used in this project. Adhering to these guidelines will ensure a consistent and maintainable UI.

---

## Core Philosophy: Core vs. Component Styles

Our styling architecture is built on two main principles:

1.  **`CoreStyles.uss` is for global styles:** This file, located in `Assets/_App/UI-Toolkit/Common/Styles/`, contains the foundational styles for the entire application. It defines the look and feel of common elements like buttons, typography, and basic layouts. These styles are designed to be generic and reusable everywhere.
2.  **Component styles are for specific components:** If a style is only used by a single component, it does not belong in `CoreStyles.uss`. Instead, it should be placed in a dedicated `.uss` file that lives alongside its corresponding component UXML file. This keeps our core stylesheet clean and makes components self-contained and modular.

---

## Core Styles (`CoreStyles.uss`)

Below is a summary of the most common generic styles you can use from the core stylesheet.

### Layout & Panels

- `.glassmorphic-panel`: The standard background panel for most windows, featuring a semi-transparent, blurred effect.
- `.content-column-panel`: A flexible column container with padding and a border, useful for segmenting content.
- `.form-layout-stretched`: A container that stretches to fill available space, perfect for forms and other content areas.
- `.button-group-row-spaced`: A flex container that arranges buttons in a row with space between them.
- `.button-container-column`: A flex container that arranges elements (typically buttons) in a full-width vertical column.

### Typography

The core stylesheet provides a set of classes for standardizing text.

- `.h1`: The largest header size (24px).
- `.h2`: A secondary header size (18px).
- `.h3`: The smallest header size (16px).
- `.title-label`: A small, bold, centered label for titles.
- `.text-wrap-normal`: Allows text to wrap to the next line.

### Buttons

A base `.button` class provides common styling (padding, font size, etc.). Modifier classes are used for color variations.

- `.button .button-primary`: The standard action button.
- `.button .button-secondary`: A button for secondary actions, like "Cancel" or "Back".
- `.icon-button`: A small, circular button intended for use with an icon.

### Icons (`Icons.uss`)

To maintain a consistent iconography across the application, a dedicated `Icons.uss` stylesheet is provided. This file contains a collection of utility classes that apply a specific SVG icon as a background image.

These classes are typically used in conjunction with the `.icon-button` style.

- **Usage:** Add the appropriate `.icon-*` class to your button or visual element.
- **Example:** `<Button class="icon-button icon-refresh" />`

A few examples of available icons include:

- `.icon-refresh`: A refresh symbol.
- `.icon-delete`: A trash can for delete actions.
- `.icon-star`: An outline of a star.
- `.icon-add`: A circle with a plus sign.

### Form Fields

- `.form-field`: The standard style for all text input fields, including `TextField`.

---

## Component-Specific Stylesheets

For any styles that are not generic and reusable, create a dedicated stylesheet for your component.

### Example: `UserListItemComponent.uss`

The `UserSelectionComponent` displays a list of users. Each item in the list is a `UserListItemComponent`. The styles for these items are unique to this component, so they live in `Assets/_App/UI-Toolkit/User Login Window/Components/UserListItemComponent.uss`.

This stylesheet includes classes like:

- `.user-item`: The main container for a single list item.
- `.user-item-image`: Styles for the user's profile picture.
- `.user-item-name-label`: Styles for the user's name.

By keeping these styles separate, we avoid cluttering `CoreStyles.uss` with highly specific rules that aren't needed anywhere else. This is the pattern you should follow for all new components.
