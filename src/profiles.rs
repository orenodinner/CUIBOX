use std::collections::HashMap;

use anyhow::{Context, Result};
use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct Profile {
    pub name: String,
    pub command: String,
    #[serde(default)]
    pub args: Vec<String>,
    #[serde(default)]
    pub cwd: Option<String>,
    #[serde(default)]
    pub env: HashMap<String, String>,
    #[serde(default)]
    pub scrollback: Option<usize>,
}

#[derive(Debug, Deserialize)]
#[serde(untagged)]
enum ProfilesFile {
    List(Vec<Profile>),
    Object { profiles: Vec<Profile> },
}

impl ProfilesFile {
    fn into_profiles(self) -> Vec<Profile> {
        match self {
            Self::List(list) => list,
            Self::Object { profiles } => profiles,
        }
    }
}

pub fn load_profiles(path: &str) -> Result<Vec<Profile>> {
    let data =
        std::fs::read_to_string(path).with_context(|| format!("Failed to read {}", path))?;
    let parsed: ProfilesFile =
        serde_json::from_str(&data).with_context(|| format!("Invalid JSON: {}", path))?;
    Ok(parsed.into_profiles())
}
