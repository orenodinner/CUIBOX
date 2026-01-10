mod app;
mod profiles;
mod tabs;
mod ui;

use std::io::{self, stdout};
use std::time::Duration;

use anyhow::Result;
use crossterm::{
    event::{self, Event, KeyEventKind},
    execute,
    terminal::{disable_raw_mode, enable_raw_mode, EnterAlternateScreen, LeaveAlternateScreen},
};
use ratatui::{backend::CrosstermBackend, layout::Rect, Terminal};

use crate::app::App;
use crate::profiles::load_profiles;
use crate::ui::{draw, split};

#[tokio::main]
async fn main() -> io::Result<()> {
    enable_raw_mode()?;
    let mut stdout = stdout();
    execute!(stdout, EnterAlternateScreen)?;
    let backend = CrosstermBackend::new(stdout);
    let mut terminal = Terminal::new(backend)?;

    let result = run_app(&mut terminal).await;

    disable_raw_mode()?;
    execute!(terminal.backend_mut(), LeaveAlternateScreen)?;
    terminal.show_cursor()?;

    if let Err(err) = result {
        eprintln!("{err:?}");
    }

    Ok(())
}

async fn run_app(terminal: &mut Terminal<CrosstermBackend<io::Stdout>>) -> Result<()> {
    let profiles = load_profiles("profiles.json")?;
    let mut app = App::new(profiles)?;
    let mut should_quit = false;

    while !should_quit {
        let size = terminal.size()?;
        let area = Rect::new(0, 0, size.width, size.height);
        let layout = split(area);
        app.resize_all(layout.pane_area);

        terminal.draw(|frame| {
            let layout = split(frame.area());
            draw(frame, &app, &layout);
        })?;

        if event::poll(Duration::from_millis(16))? {
            match event::read()? {
                Event::Key(key) => {
                    if key.kind != KeyEventKind::Press {
                        continue;
                    }
                    app.handle_key(key, &mut should_quit).await?;
                }
                Event::Paste(text) => {
                    app.handle_paste(&text).await?;
                }
                Event::Resize(_, _) => {
                    app.handle_resize();
                }
                _ => {}
            }
        }
    }

    Ok(())
}
