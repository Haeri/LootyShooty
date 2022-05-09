using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System;

public class Entrypoint : MonoBehaviour
{

    public string configFile = "game_config.txt";

    public class Config
    {
        public string ip = "127.0.0.1";
        public int port = 7777;
    }

    private void Start()
    {
        if (Application.isBatchMode)
        {
            Config cfg = new Config();

            if (File.Exists(configFile))
            {
                cfg = ReadConfig();
            }
            else
            {
                Debug.Log("INFO: No Config detected, cerating a default " + configFile);
                WriteDefaultConfig();
            }

            ConnectionManager.Instance.StartupServer(cfg.ip, cfg.port);
        }
    }

    void WriteDefaultConfig()
    {
        Config cfg = new Config();

        StreamWriter writer = new StreamWriter(configFile, false);
        FieldInfo[] configFields = cfg.GetType().GetFields();
        foreach (FieldInfo item in configFields)
        {
            writer.WriteLine($"{item.Name}={ item.GetValue(cfg)}");
        }

        writer.Close();
    }

    
    Config ReadConfig()
    {
        StreamReader reader = new StreamReader(configFile);
        string fileContent = reader.ReadToEnd();
        reader.Close();

        Config cfg = new Config();

        FieldInfo[] configFields = cfg.GetType().GetFields();
        foreach (FieldInfo item in configFields)
        {
            Regex regex = new Regex(@""+ item.Name + "=(.*)");
            Match match = regex.Match(fileContent);
            if (match.Success)
            {
                Debug.Log("INFO: "+item.Name+" = "+match.Groups[1].Value);
                item.SetValue(cfg, Convert.ChangeType(match.Groups[1].Value, item.FieldType));
            }
        }

        return cfg;
    }

}
