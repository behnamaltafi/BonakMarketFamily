// CI-only adapter for CICD_Platform (pinned GitHub revision).
// The installed Jenkins global library remains untouched. Its current Groovy
// implementation cannot compile, so we invoke the platform's official CLI stages.
pipeline {
    agent { label 'linux && dotnet10 && ci' }

    options {
        skipDefaultCheckout()
        timestamps()
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '40'))
    }

    environment {
        CICD_CONFIG = 'cicd/project.json'
        CICD_BASE_REF = 'origin/master'
        CICD_PLATFORM_REVISION = '1e2c209528818f5940c63998668cdeac4bffb66e'
        // Make the bundled .NET 9 ModuleBuilder usable on the .NET 10-only CI agent.
        DOTNET_ROLL_FORWARD = 'Major'
        // Reduce compilation pressure on the CI agent (2.25 GiB cgroup limit).
        UseSharedCompilation = 'false'
        BuildInParallel = 'false'
        RunAnalyzersDuringBuild = 'false'
        MSBUILDDISABLENODEREUSE = '1'
        DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
    }

    stages {
        stage('Checkout source') {
            steps {
                deleteDir()
                checkout scm
            }
        }

        stage('Pinned CICD_Platform tooling') {
            steps {
                dir('.cicd-platform') {
                    checkout([
                        $class: 'GitSCM',
                        branches: [[name: '1e2c209528818f5940c63998668cdeac4bffb66e']],
                        userRemoteConfigs: [[
                            url: 'https://github.com/behnamaltafi/CICD_Platform.git',
                            credentialsId: 'platform-github-read'
                        ]]
                    ])
                    script {
                        def actual = sh(script: 'git rev-parse HEAD', returnStdout: true).trim()
                        if (actual != env.CICD_PLATFORM_REVISION) {
                            error('Unapproved CICD_Platform tooling revision')
                        }
                    }
                }
            }
        }

        stage('Validate configuration') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs validate "$CICD_CONFIG"' }
        }

        stage('Conventional commits') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs ci commits' }
        }

        stage('Restore') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs ci restore' }
        }

        stage('Build') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs ci build' }
        }

        stage('Unit tests') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs ci test' }
        }

        stage('Coverage validation') {
            steps { sh 'node .cicd-platform/scripts/cli.mjs ci coverage' }
        }
    }

    post {
        always {
            archiveArtifacts artifacts: '.cicd-output/**', allowEmptyArchive: true, fingerprint: true
        }
    }
}
