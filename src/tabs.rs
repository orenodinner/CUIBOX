use cockpit::PaneId;

use crate::profiles::Profile;

#[derive(Clone)]
pub struct Tab {
    pub profile: Profile,
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

    pub fn is_empty(&self) -> bool {
        self.tabs.is_empty()
    }

    pub fn active(&self) -> usize {
        self.active
    }

    pub fn active_tab(&self) -> Option<&Tab> {
        self.tabs.get(self.active)
    }

    pub fn active_pane_id(&self) -> Option<PaneId> {
        self.active_tab().map(|tab| tab.pane_id)
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
