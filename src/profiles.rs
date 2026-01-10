use std::collections::HashMap;

use anyhow::{Context, Result};
use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct Profile {
    pub name: String,
    #[serde(default)]
    pub command: Option<String>,
    #[serde(default)]
    pub args: Vec<String>,
    #[serde(default)]
    pub cwd: Option<String>,
    #[serde(default)]
    pub env: HashMap<String, String>,
}

#[derive(Debug, Deserialize)]
struct ProfilesFile {
    profiles: Vec<Profile>,
}

pub fn load_profiles(path: &str) -> Result<Vec<Profile>> {
    let data = std::fs::read_to_string(path)
        .with_context(|| format!("{} の読み込みに失敗しました。", path))?;
    let parsed: ProfilesFile =
        serde_json::from_str(&data).with_context(|| format!("{} のJSON形式が不正です。", path))?;
    if parsed.profiles.is_empty() {
        anyhow::bail!("{} に profiles が定義されていません。", path);
    }
    Ok(parsed.profiles)
}
