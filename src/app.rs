use std::path::PathBuf;

use anyhow::{bail, Result};
use cockpit::{PaneHandle, PaneSize, SpawnConfig};
use crossterm::event::{KeyCode, KeyEvent, KeyModifiers};
use ratatui::layout::Rect;

use crate::profiles::Profile;
use crate::tabs::{new_tab, TabsState};

pub struct App {
    profiles: Vec<Profile>,
    tabs: TabsState,
    last_pane_size: Option<(u16, u16)>,
    next_profile_index: usize,
}

impl App {
    pub fn new(profiles: Vec<Profile>) -> Result<Self> {
        if profiles.is_empty() {
            bail!("profiles.json に profiles が定義されていません。");
        }
        let mut tabs = Vec::with_capacity(profiles.len());

        for (index, profile) in profiles.iter().cloned().enumerate() {
            let tab = spawn_tab_from_profile(&profile, Some(index))?;
            tabs.push(tab);
        }

        let tabs = TabsState::new(tabs);
        let mut app = Self {
            profiles,
            tabs,
            last_pane_size: None,
            next_profile_index: 0,
        };
        app.focus_active();

        Ok(app)
    }

    pub fn tabs(&self) -> &TabsState {
        &self.tabs
    }

    pub fn active_handle(&self) -> Option<&PaneHandle> {
        self.tabs
            .active_tab()
            .and_then(|tab| tab.manager.get_pane(tab.pane_id))
    }

    pub fn active_profile_name(&self) -> Option<&str> {
        self.tabs.active_tab().map(|tab| tab.title.as_str())
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
        for tab in self.tabs.tabs_mut() {
            let _ = tab.manager.resize_pane(tab.pane_id, pane_size);
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

        if key.code == KeyCode::Char('t') && key.modifiers.contains(KeyModifiers::CONTROL) {
            self.add_tab_from_next_profile()?;
            return Ok(());
        }

        if key.code == KeyCode::Char('w') && key.modifiers.contains(KeyModifiers::CONTROL) {
            self.close_active_tab();
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

        if let Some(tab) = self.tabs.active_tab() {
            tab.manager.route_key(key).await?;
        }
        Ok(())
    }

    pub async fn handle_paste(&mut self, text: &str) -> Result<()> {
        if let Some(tab) = self.tabs.active_tab() {
            tab.manager.send_input(text.as_bytes()).await?;
        }
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
        if let Some(tab) = self.tabs.active_tab_mut() {
            tab.manager.set_focus(tab.pane_id);
        }
    }

    fn add_tab_from_next_profile(&mut self) -> Result<()> {
        if self.profiles.is_empty() {
            return Ok(());
        }
        let index = self.next_profile_index % self.profiles.len();
        let profile = self.profiles[index].clone();
        self.next_profile_index = (index + 1) % self.profiles.len();

        let tab = spawn_tab_from_profile(&profile, Some(index))?;
        self.tabs.add_tab(tab);
        let last_index = self.tabs.tabs().len().saturating_sub(1);
        self.tabs.set_active(last_index);
        self.focus_active();
        Ok(())
    }

    fn close_active_tab(&mut self) {
        if self.tabs.is_empty() {
            return;
        }
        let active = self.tabs.active();
        let _ = self.tabs.remove_tab(active);
        if !self.tabs.is_empty() {
            self.focus_active();
        }
    }
}

fn spawn_tab_from_profile(profile: &Profile, profile_id: Option<usize>) -> Result<crate::tabs::Tab> {
    let mut config = match &profile.command {
        Some(command) => SpawnConfig::new_command(command.clone()).args(profile.args.clone()),
        None => SpawnConfig::new_shell(),
    };

    if let Some(cwd) = &profile.cwd {
        config = config.cwd(PathBuf::from(cwd));
    }
    for (key, value) in &profile.env {
        config = config.env(key, value);
    }

    new_tab(profile.name.clone(), profile_id, config)
}
