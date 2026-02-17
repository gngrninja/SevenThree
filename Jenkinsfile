pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
  }

  environment {
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"
  }

  stages {
    stage('Build') {
      steps {
        sh 'dotnet build -c Release'
      }
    }

    stage('Test') {
      steps {
        sh '''
          dotnet test -c Release --no-build \
            --logger "trx;LogFileName=test_results.trx" \
            --results-directory "${WORKSPACE}/TestResults"
        '''
      }
      post {
        always {
          archiveArtifacts artifacts: 'TestResults/**/*', allowEmptyArchive: true
        }
      }
    }

    stage('Deploy') {
      when {
        allOf {
          buildingTag()
          tag pattern: "v\\d+\\.\\d+\\.\\d+", comparator: "REGEXP"
        }
      }
      steps {
        script {
          env.TAG_NAME = env.BRANCH_NAME ?: env.GIT_BRANCH?.replace('refs/tags/', '')?.replaceAll('.*/tags/', '')
        }
        sh '''
          echo "Deploying SevenThree version ${TAG_NAME}..."

          if [ -f /var/lib/jenkins/seventhree.env ]; then
            . /var/lib/jenkins/seventhree.env
            export SEVENTHREE_DEPLOY_DIR SEVENTHREE_DEPLOY_USER SEVENTHREE_DEPLOY_HOST
          else
            echo "Error: /var/lib/jenkins/seventhree.env not found"
            exit 1
          fi

          chmod +x ./deploy.sh
          ./deploy.sh
        '''
      }
    }
  }

  post {
    success {
      emailext(
        from: "${env.EMAIL_FROM}",
        to: "${env.EMAIL_TO}",
        subject: "SUCCESS: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
        body: """
        <p><b>Build succeeded</b></p>
        <p><b>Job:</b> ${env.JOB_NAME}</p>
        <p><b>Build:</b> #${env.BUILD_NUMBER}</p>
        <p><a href="${env.BUILD_URL}">Open build</a></p>
        """,
        mimeType: 'text/html',
        attachLog: true
      )
    }
    failure {
      emailext(
        from: "${env.EMAIL_FROM}",
        to: "${env.EMAIL_TO}",
        subject: "FAILURE: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
        body: """
        <p><b>Build failed</b></p>
        <p><b>Job:</b> ${env.JOB_NAME}</p>
        <p><b>Build:</b> #${env.BUILD_NUMBER}</p>
        <p><a href="${env.BUILD_URL}">Open logs</a></p>
        """,
        mimeType: 'text/html',
        attachLog: true
      )
    }
    always {
      cleanWs(deleteDirs: true, notFailBuild: true)
    }
  }
}
