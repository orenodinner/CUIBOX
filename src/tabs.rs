use anyhow::{Context, Result};
use cockpit::{ManagerConfig, PaneId, PaneManager, SpawnConfig};

pub struct Tab {
    pub title: String,
    pub profile_id: Option<usize>,
    pub manager: PaneManager,
    pub pane_id: PaneId,
}

pub struct TabsState {
    tabs: Vec<Tab>,
    active: usize,
}

impl TabsState {
    pub fn new(tabs: Vec<Tab>) -> Self {
        Self { tabs, active: 0 }
    }

    pub fn tabs(&self) -> &[Tab] {
        &self.tabs
    }

    pub fn tabs_mut(&mut self) -> &mut [Tab] {
        &mut self.tabs
    }

    pub fn is_empty(&self) -> bool {
        self.tabs.is_empty()
    }

    pub fn active(&self) -> usize {
        self.active
    }

    pub fn set_active(&mut self, index: usize) {
        if self.tabs.is_empty() {
            self.active = 0;
            return;
        }
        self.active = index.min(self.tabs.len() - 1);
    }

    pub fn active_tab(&self) -> Option<&Tab> {
        self.tabs.get(self.active)
    }

    pub fn active_tab_mut(&mut self) -> Option<&mut Tab> {
        self.tabs.get_mut(self.active)
    }

    pub fn add_tab(&mut self, tab: Tab) {
        self.tabs.push(tab);
        if self.tabs.len() == 1 {
            self.active = 0;
        }
    }

    pub fn remove_tab(&mut self, index: usize) -> Option<Tab> {
        if index >= self.tabs.len() {
            return None;
        }
        let removed = self.tabs.remove(index);
        if self.tabs.is_empty() {
            self.active = 0;
        } else if index < self.active {
            self.active -= 1;
        } else if index == self.active && self.active >= self.tabs.len() {
            self.active = self.tabs.len() - 1;
        }
        Some(removed)
    }

    pub fn next(&mut self) {
        if self.tabs.is_empty() {
            return;
        }
        self.active = (self.active + 1) % self.tabs.len();
    }

    pub fn prev(&mut self) {
        if self.tabs.is_empty() {
            return;
        }
        if self.active == 0 {
            self.active = self.tabs.len() - 1;
        } else {
            self.active -= 1;
        }
    }
}

pub fn new_tab(
    title: String,
    profile_id: Option<usize>,
    spawn: SpawnConfig,
) -> Result<Tab> {
    let config = ManagerConfig {
        max_panes: 1,
        ..Default::default()
    };
    let mut manager = PaneManager::with_config(config);
    let handle = manager
        .spawn(spawn)
        .with_context(|| format!("{} の起動に失敗しました。", title))?;
    let pane_id = handle.id();
    manager.set_focus(pane_id);
    Ok(Tab {
        title,
        profile_id,
        manager,
        pane_id,
    })
}
