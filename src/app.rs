use std::path::PathBuf;

use anyhow::{bail, Context, Result};
use cockpit::{PaneHandle, PaneManager, PaneSize, SpawnConfig};
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::Rect;

use crate::profiles::Profile;
use crate::tabs::{Tab, TabsState};

pub struct App {
    manager: PaneManager,
    tabs: TabsState,
    last_pane_size: Option<(u16, u16)>,
}

impl App {
    pub fn new(profiles: Vec<Profile>) -> Result<Self> {
        if profiles.is_empty() {
            bail!("profiles.json has no profiles");
        }
        if profiles.len() > 4 {
            bail!("This MVP supports up to 4 profiles");
        }

        let mut manager = PaneManager::new();
        let mut tabs = Vec::with_capacity(profiles.len());

        for profile in profiles {
            let mut config = SpawnConfig::new_command(profile.command.clone())
                .args(profile.args.clone());

            if let Some(cwd) = &profile.cwd {
                config = config.cwd(PathBuf::from(cwd));
            }
            for (key, value) in &profile.env {
                config = config.env(key, value);
            }
            if let Some(scrollback) = profile.scrollback {
                config = config.scrollback(scrollback);
            }

            let handle = manager
                .spawn(config)
                .with_context(|| format!("Failed to spawn {}", profile.name))?;
            tabs.push(Tab {
                profile,
                pane_id: handle.id(),
            });
        }

        let tabs = TabsState::new(tabs);
        let mut app = Self {
            manager,
            tabs,
            last_pane_size: None,
        };
        app.focus_active();

        Ok(app)
    }

    pub fn manager(&self) -> &PaneManager {
        &self.manager
    }

    pub fn tabs(&self) -> &TabsState {
        &self.tabs
    }

    pub fn active_handle(&self) -> Option<&PaneHandle> {
        self.tabs
            .active_pane_id()
            .and_then(|pane_id| self.manager.get_pane(pane_id))
    }

    pub fn active_profile_name(&self) -> Option<&str> {
        self.tabs.active_tab().map(|tab| tab.profile.name.as_str())
    }

    pub fn resize_all(&mut self, area: Rect) {
        let rows = area.height.saturating_sub(2);
        let cols = area.width.saturating_sub(2);
        if rows == 0 || cols == 0 {
            return;
        }
        let size = (rows, cols);
        if self.last_pane_size == Some(size) {
            return;
        }
        self.last_pane_size = Some(size);

        let pane_size = PaneSize::new(rows, cols);
        for tab in self.tabs.tabs() {
            let _ = self.manager.resize_pane(tab.pane_id, pane_size);
        }
    }

    pub fn handle_resize(&mut self) {
        self.last_pane_size = None;
    }

    pub async fn handle_key(&mut self, key: KeyEvent, should_quit: &mut bool) -> Result<()> {
        if key.code == KeyCode::Char('q') && key.modifiers.contains(KeyModifiers::CONTROL) {
            *should_quit = true;
            return Ok(());
        }

        if key.code == KeyCode::Char('n') && key.modifiers.contains(KeyModifiers::CONTROL) {
            self.next_tab();
            return Ok(());
        }

        if key.code == KeyCode::Char('p') && key.modifiers.contains(KeyModifiers::CONTROL) {
            self.prev_tab();
            return Ok(());
        }

        self.manager.route_key(key).await?;
        Ok(())
    }

    pub async fn handle_paste(&mut self, text: &str) -> Result<()> {
        self.manager.send_input(text.as_bytes()).await?;
        Ok(())
    }

    fn next_tab(&mut self) {
        self.tabs.next();
        self.focus_active();
    }

    fn prev_tab(&mut self) {
        self.tabs.prev();
        self.focus_active();
    }

    fn focus_active(&mut self) {
        if let Some(pane_id) = self.tabs.active_pane_id() {
            self.manager.set_focus(pane_id);
        }
    }
}
