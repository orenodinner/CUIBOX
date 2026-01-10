use cockpit::PaneState;
use ratatui::{
    layout::{Constraint, Direction, Layout, Rect},
    style::{Modifier, Style},
    text::Line,
    widgets::{Block, Borders, Paragraph, Tabs},
    Frame,
};

use crate::app::App;

pub struct UiLayout {
    pub tab_area: Rect,
    pub pane_area: Rect,
}

pub fn split(area: Rect) -> UiLayout {
    let chunks = Layout::default()
        .direction(Direction::Vertical)
        .constraints([Constraint::Length(3), Constraint::Min(0)])
        .split(area);
    UiLayout {
        tab_area: chunks[0],
        pane_area: chunks[1],
    }
}

pub fn draw(frame: &mut Frame, app: &App, layout: &UiLayout) {
    let titles = tab_titles(app);
    let selected = if app.tabs().is_empty() {
        None
    } else {
        Some(app.tabs().active())
    };

    let tabs = Tabs::new(titles)
        .select(selected)
        .block(Block::default().borders(Borders::ALL).title("Profiles"))
        .highlight_style(Style::default().add_modifier(Modifier::BOLD));
    frame.render_widget(tabs, layout.tab_area);

    if let Some(handle) = app.active_handle() {
        let title = app
            .active_profile_name()
            .map(|name| format!(" {} ", name))
            .unwrap_or_else(|| " Active ".to_string());
        let block = Block::default().borders(Borders::ALL).title(title);
        let widget = cockpit::PaneWidget::new(handle).focused(true).block(block);
        frame.render_widget(widget, layout.pane_area);
    } else {
        let placeholder =
            Paragraph::new("Pane not available").block(Block::default().borders(Borders::ALL));
        frame.render_widget(placeholder, layout.pane_area);
    }
}

fn tab_titles(app: &App) -> Vec<Line> {
    app.tabs()
        .tabs()
        .iter()
        .map(|tab| {
            let state = app
                .manager()
                .get_pane(tab.pane_id)
                .map(|pane| pane.state())
                .unwrap_or(PaneState::Exited { code: -1 });
            Line::from(format!(
                " {} [{}] ",
                tab.profile.name,
                state_label(state)
            ))
        })
        .collect()
}

fn state_label(state: PaneState) -> &'static str {
    match state {
        PaneState::Running => "running",
        PaneState::Exited { .. } => "exited",
        PaneState::Crashed { .. } => "crashed",
        PaneState::Paused => "paused",
    }
}
